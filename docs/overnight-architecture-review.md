# Overnight Architecture Review

## 1. Executive Summary

This review inspected both executable projects, domain/application/adapter code, persistence, tests, ADRs, manifests, configuration, and recent history. The repository contains an append-only bank-account event-sourcing demo and a CAP/EF Core checkout workflow intended to demonstrate hexagonal architecture.

The selected weakness was a dependency inversion violation in the checkout write path. `CheckoutUseCase` directly depended on `EcommerceDbContext`, while an application-owned transaction interface exposed EF Core's `DbContext` and `IDbContextTransaction`. The change adds narrow application-owned order-store and transaction ports; EF Core and CAP details now live in secondary adapters. The observable order remains: create and sequence event, begin transaction, store order, publish with order partition key, commit. A static architecture test makes this boundary executable. Compilation was unavailable because this environment has no .NET CLI.

## 2. Repository Architecture Observed

* **Bank-account web demo:** ASP.NET Core presentation over an append-only in-memory event store. Domain commands produce immutable envelope-backed events; `DemoStateService` coordinates command handling, persistence, projection, and replay.
* **Checkout domain:** owns `Order`, `CartItem`, events, envelope metadata, and partition semantics without persistence responsibilities.
* **Checkout application:** owns the checkout use case, CAP subscribers, replay projector, and outbound ports.
* **Primary adapters:** CLI/configuration demo and HTTP CAP samples.
* **Secondary adapters:** in-memory external effects, CAP publication, and EF Core/SQLite persistence for orders, deduplication, producer sequences, and consumer guards.
* **Composition root:** reads environment configuration, registers dependencies, configures RabbitMQ/SQLite/CAP, initializes storage, runs samples, and starts ASP.NET Core.

Most checkout effects already follow ports and adapters. Reliability is cross-cutting and explicit in envelopes, partition affinity, deduplication, sequence guards, and replay-only projection. Errors normally propagate; payment exceptions are converted to failure events. Tests combine behavior checks with source-scanning architecture fitness functions. No deployment definitions beyond runtime configuration were found.

## 3. Candidate Issues

| Rank | Issue | Evidence | Impact | Risk | Selected |
|---:|---|---|---|---|:---:|
| 1 | Application write path depends on EF adapter types | Use case imported secondary persistence; application port exposed EF types | Reverses hexagonal dependency; infrastructure-heavy tests and change amplification | Low | Yes |
| 2 | Sequence allocation commits before order/CAP transaction | Allocator saves before transaction begins | Failed checkout consumes sequence; weak atomicity | Medium | No |
| 3 | Deduplication is recorded before effects complete | Handlers mark then invoke adapters | Crash window can lose an effect | High | No |
| 4 | Catch-all exception becomes payment business failure | Payment handler catches `Exception` | Transient faults may not retry | Medium | No |
| 5 | First consumer sequence is accepted without baseline | Guard accepts any first sequence | Ordering guarantee is ambiguous | Medium | No |
| 6 | Wildcard package versions | E-commerce project uses `Version="*"` | Non-reproducible restores/upgrades | Low | No |
| 7 | Demo writes are coupled to startup | `Program.cs` places sample orders before serving | Restart can fail on duplicate IDs | Medium | No |
| 8 | Handler safety protocol is duplicated | Only some subscribers share a helper | Policy changes amplify and ordering is misusable | Medium | No |
| 9 | `EnsureCreated` coexists with standalone schema SQL | Two schema-evolution mechanisms | No single migration source of truth | Medium | No |
| 10 | Two demos share one test assembly | Tests reference both apps and scan source paths | Broad build coupling | Low | No |

## 4. Selected Architectural Problem

The call path was `CheckoutCliAdapter -> CheckoutUseCase -> EcommerceDbContext/OrderRecord + ICapTransactionCoordinator(DbContext) -> CAP EF extension`. The nominal port did not hide infrastructure; it relocated infrastructure types into an application namespace. Switching persistence, changing record mapping, or replacing atomic publication therefore changed application policy as well as adapters. It contradicted both documented hexagonal rules and the established `IEventBus`/side-effect ports.

## 5. Why This Is an Architectural Issue

This was not style: a central dependency crossed from policy into a secondary adapter. The use case knew workflow policy, persistence mapping, change tracking, and transaction context, reducing cohesion. The leaky interface provided no information hiding and made technology changes affect the port, adapter, and use case. Isolated application testing also required a database context.

**Falsifiable hypothesis:** If persistence and transaction control move behind application-owned ports containing no EF Core/CAP types, persistence technology changes will no longer require changes to `CheckoutUseCase`, while write/publish/commit order and public behavior remain unchanged.

Success criteria: zero adapter/EF namespace references under `Application`; only application/domain abstractions in the use-case constructor; mapping and CAP setup confined to adapters; and a fitness test that catches regression.

## 6. Architectural Concepts Researched

### Repository plus technology-neutral unit of work — selected

`IOrderStore` hides record mapping and `ICheckoutTransactionManager` hides the CAP/EF transaction. This corrects dependency direction with a small diff and improves replaceability/fakes. Its cost is interface indirection and retained temporal `Begin`/`Commit` coupling. It fits the existing ports-and-adapters vocabulary.

### Coarse `PlaceOrderAtomically` gateway — rejected

One output port could hide storage, sequencing, publication, and atomicity. It encapsulates strongly, but either moves event policy into infrastructure or accepts awkward callbacks/specifications and becomes a use-case-shaped infrastructure service.

### Transaction pipeline/decorator — rejected

A decorator could remove explicit transaction mechanics and standardize many commands. This repository has one transactional command and no mediator pipeline; adding one creates more framework/dynamic coupling than it removes.

### Retain a pragmatic EF transaction script — rejected

This has fewer types and direct readability, but conflicts with the explicit educational goal and the boundary consistently used for every other secondary effect.

The evaluation used established information-hiding, dependency inversion, repository, and unit-of-work concepts. Live research was attempted, but the browsing service returned HTTP 401, so no live-source verification is claimed. Recommended follow-up sources are David Parnas's *On the Criteria To Be Used in Decomposing Systems into Modules* and Microsoft's .NET architecture guidance on infrastructure persistence behind application-owned interfaces.

## 7. Architecture Decision

**Context:** Checkout must persist an order and enqueue `OrderPlaced` within CAP/EF local transaction behavior while application policy remains infrastructure-independent.

**Decision:** Define `IOrderStore`, `ICheckoutTransactionManager`, and `ICheckoutTransaction` in application ports. Implement storage/mapping in `EfCoreOrderStore`, adapt CAP's EF transaction in `CapTransactionManager`, and compose both with scoped lifetimes.

**Alternatives:** Coarse gateway, transaction decorator, and direct transaction script (above).

**Consequences:** The application no longer imports adapters/EF; mapping or providers can vary behind ports. Three interface types and a wrapper add concepts; explicit transaction sequencing remains.

**Risks:** The wrapper may be too small if rollback/cancellation APIs become necessary. Runtime compatibility still needs .NET-enabled validation.

**Assumptions:** Uncommitted disposal rolls back; store and manager receive the same scoped context; CAP publication participates in that context's transaction; and no unknown external consumer relies on the removed coordinator type.

## 8. Before

```text
CheckoutUseCase (application)
  |--> EcommerceDbContext + OrderRecord (secondary adapter)
  |--> ICapTransactionCoordinator -- exposes DbContext/IDbContextTransaction
  `--> IEventBus
```

## 9. After

```text
CheckoutUseCase
  |--> IOrderStore <---------------- EfCoreOrderStore --> EcommerceDbContext
  |--> ICheckoutTransactionManager <- CapTransactionManager --> CAP + EF
  `--> IEventBus <------------------ CapEventBus --> CAP
```

Adapter implementations now point toward application-owned contracts; application policy has no adapter/framework reference.

## 10. Files Changed

* `Application/Ports/IOrderStore.cs`: application-vocabulary persistence capability.
* `Application/Ports/IEventSequencePorts.cs`: replaces the EF-leaking coordinator with neutral transaction lifecycle ports.
* `Application/UseCases/CheckoutUseCase.cs`: transacts and persists only through ports while preserving operation order.
* `Adapters/Secondary/Persistence/EfCoreOrderStore.cs`: owns domain-to-record mapping and EF save behavior.
* `Adapters/Secondary/CapTransactionManager.cs`: implements the neutral manager and wraps CAP's EF transaction; the adapter name now reflects the application capability.
* `Program.cs`: maps new ports using scoped lifetimes and resolves CLI from a scope.
* `EcommerceEventPublishingArchitectureTests.cs`: enforces dependency direction.
* `README.md`: updates the documented path and boundary.
* This report documents the decision and evidence.

## 11. Tests Added or Modified

`application_layer_must_not_depend_on_secondary_adapters_or_entity_framework` scans all checkout application C# files and reports relative paths containing adapter or EF namespaces. It guards the corrected boundary and complements existing CAP publication fitness functions. No behavioral test was weakened or removed.

## 12. Validation Results

* Repository/instruction discovery completed; no `AGENTS.md` was found.
* Static dependency search completed after implementation; no application C# file references adapter or EF namespaces.
* `dotnet test EventSourcing.sln --no-restore` could not execute: `dotnet: command not found`.
* Build, unit/integration execution, analyzers, and runtime validation are therefore not claimed as passing.
* Live architectural-source search was attempted but returned HTTP 401.

## 13. Before vs After

Before, two application files coupled to infrastructure: the use case imported persistence and the port imported EF. After, static search finds zero such references. EF record mapping changes are isolated to persistence; transaction-provider changes are isolated to its adapter/composition root. Runtime steps, event schemas, topics, partition keys, and database schema are unchanged.

Coupling was not hidden in reflection or service location: constructor dependencies remain explicit. Runtime coupling to one shared scoped context necessarily remains for atomic CAP/EF behavior.

## 14. Trade-offs Introduced

Three interfaces, one adapter, and one private wrapper increase navigation and conceptual count. The API remains temporally coupled: omitting `Commit` rolls back rather than causing a compile error. Wrapper overhead is negligible. Removing the old coordinator is source-breaking for hypothetical external consumers, though this executable demo shows none.

## 15. Self-Review Findings

Adversarial review rejected the initially considered atomic gateway because it would move workflow policy into infrastructure or require callbacks. Review then found a lifecycle mismatch: scoped EF ports could not safely be consumed by singleton use-case/CLI registrations. Both were changed to scoped and startup now creates a scope. Finally, the architecture fitness test was added so the decision is maintained rather than merely documented.

The final diff was checked for operation ordering, cancellation propagation, mapping ownership, transaction disposal, namespace direction, unrelated formatting, secrets, event/database contracts, and error behavior.

## 16. Remaining Architectural Risks

Sequence allocation remains outside the transaction; mark-before-effect deduplication has a crash window; payment error taxonomy is too broad; consumer ordering baseline is ambiguous; dependencies are wildcarded; startup mutates state; schema evolution is split; and subscriber reliability flow remains partly duplicated.

## 17. Recommended Next Improvements

1. Make producer sequence allocation atomic with order/publish and prove concurrency behavior.
2. Translate only known business declines to `PaymentFailed`; allow transient faults to retry.
3. Make consumer effects crash-safe via an inbox/effect-outbox or explicitly tested adapter idempotency contracts.

These are intentionally not implemented because each changes reliability semantics and needs broader integration evidence.

## 18. Architecture Lesson

Dependency inversion is about **who owns the contract**, not whether an interface exists. An application interface mentioning `DbContext` still depends on EF's model. A useful port speaks stable policy language (`AddAsync(Order)`, `Begin`, `Commit`) and leaves volatile representation (`OrderRecord`, change tracking, CAP extensions) to adapters.

Use this where a meaningful policy/technology boundary exists and replaceability, isolated testing, or conceptual integrity matters. Do not create a port for every class: an abstraction that hides nothing can add accidental complexity. It fits here because this project explicitly teaches hexagonal architecture and isolates every other secondary effect.

A common misconception is that inversion eliminates runtime coupling. Checkout still needs storage and CAP; inversion changes static knowledge and contract ownership so technology can vary without rewriting policy.

**Interview question:** In a checkout that atomically persists an order and publishes an event, where should the transaction boundary live, who owns its interface, and how would you prove atomicity without leaking persistence framework types into policy?

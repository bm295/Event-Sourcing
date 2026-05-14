## Event Guardrail Checklist

- [ ] Event mới có `OrderId` routing key chưa?
- [ ] Publish path có bằng chứng partition/stream affinity chưa?
- [ ] Có test ordering theo aggregate chưa?
- [ ] Các call publish domain event đều đi qua `IEventBus` (không gọi trực tiếp raw publisher ngoài adapter layer).

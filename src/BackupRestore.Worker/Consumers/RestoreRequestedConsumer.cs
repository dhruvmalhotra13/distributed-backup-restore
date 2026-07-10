using BackupRestore.Core.Contracts;
using BackupRestore.Worker.Services;
using MassTransit;

namespace BackupRestore.Worker.Consumers;

public class RestoreRequestedConsumer : IConsumer<RestoreRequested>
{
    private readonly RestoreProcessor _processor;

    public RestoreRequestedConsumer(RestoreProcessor processor)
    {
        _processor = processor;
    }

    public Task Consume(ConsumeContext<RestoreRequested> context)
        => _processor.ProcessAsync(context.Message, context.CancellationToken);
}

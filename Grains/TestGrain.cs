using System.Diagnostics;
using Sandbox.Domain;
using Sandbox.Repositories;
using LogMessages = Sandbox.Logging.LogMessages;

namespace Sandbox.Grains;

public interface ITestGrain : IGrainWithStringKey
{
    Task StartSaga(CancellationToken cancellationToken);
}

public class TestGrain : Grain, ITestGrain, IRemindable
{
    private SagaContext? _sagaContext;
    private readonly string _key;
    private static readonly TimeSpan WakeUpReminderPeriod = TimeSpan.FromMinutes(1);
    private readonly SagaContextRepository _sagaContextRepository;
    private readonly ILogger<TestGrain> _logger;

    public TestGrain(SagaContextRepository contextRepository, ILogger<TestGrain> logger)
    {
        _sagaContextRepository = contextRepository;
        _logger = logger;
        _key = this.GetPrimaryKeyString();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        await this.RegisterOrUpdateReminder(
            "WakeUpReminder",
            WakeUpReminderPeriod,
            WakeUpReminderPeriod);

        _sagaContext = await _sagaContextRepository.Get(_key, cancellationToken);
        if (_sagaContext != null)
        {
            using var activity = Activity.Current?.Source.StartActivity(
                name: "SagaWakeUp", 
                kind: ActivityKind.Internal, 
                // Установка связи
                parentId: _sagaContext.BaseActivityId);

            LogMessages.SagaContinued(_logger);
        }
    }

    public async Task StartSaga(CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        LogMessages.SagaStarted(_logger);
        _sagaContext = new SagaContext
        {
            // Сохранение id активити
            BaseActivityId = activity?.Id,
            Id = _key
        };
        await _sagaContextRepository.Set(_sagaContext, cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        return Task.CompletedTask;
    }
}
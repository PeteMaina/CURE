namespace CURE.Domain.Tests.Pipeline;

using CURE.Application.CoreCrm;
using CURE.Application.Pipeline;
using CURE.Domain.Shared;
using Xunit;

public sealed class PipelineServiceTests
{
    private readonly FakePipelineStore _store = new();
    private readonly PipelineService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public PipelineServiceTests()
    {
        _service = new PipelineService(_store);
    }

    [Fact]
    public async Task TransitionStage_ThrowsWhenTargetStageIsEmpty()
    {
        var command = new TransitionStageCommand("   ");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.TransitionStageAsync(_tenantId, _actorId, "req-1", Guid.NewGuid(), command, CancellationToken.None));

        Assert.Equal(ErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task TransitionStage_NormalizesInputsAndCallsStore()
    {
        var oppId = Guid.NewGuid();
        var command = new TransitionStageCommand(" Proposal ", " BestValue ", " CompetitorX ", " Some notes ");

        var result = await _service.TransitionStageAsync(_tenantId, _actorId, "req-2", oppId, command, CancellationToken.None);

        Assert.NotNull(_store.LastTransitionCommand);
        Assert.Equal("Proposal", _store.LastTransitionCommand.TargetStage);
        Assert.Equal("BestValue", _store.LastTransitionCommand.CloseReason);
        Assert.Equal("CompetitorX", _store.LastTransitionCommand.CompetitorName);
        Assert.Equal("Some notes", _store.LastTransitionCommand.Notes);
        Assert.Equal(oppId, _store.LastTransitionOppId);
    }

    [Fact]
    public async Task ConvertLead_NormalizesCurrencyAndDefaultsCustomerType()
    {
        var leadId = Guid.NewGuid();
        var command = new ConvertLeadCommand(
            CustomerType: " ",
            CustomerName: " Acme Corp ",
            CreateOpportunity: true,
            OpportunityName: " Expansion ",
            OpportunityAmount: 50_000m,
            Currency: " kes ");

        var result = await _service.ConvertLeadAsync(_tenantId, _actorId, "req-3", leadId, command, CancellationToken.None);

        Assert.NotNull(_store.LastConvertCommand);
        Assert.Equal("Business", _store.LastConvertCommand.CustomerType);
        Assert.Equal("Acme Corp", _store.LastConvertCommand.CustomerName);
        Assert.Equal("Expansion", _store.LastConvertCommand.OpportunityName);
        Assert.Equal("KES", _store.LastConvertCommand.Currency);
    }

    private sealed class FakePipelineStore : IPipelineStore
    {
        public TransitionStageCommand? LastTransitionCommand { get; private set; }
        public Guid? LastTransitionOppId { get; private set; }
        public ConvertLeadCommand? LastConvertCommand { get; private set; }

        public Task<PipelineSummaryDto> GetPipelineSummaryAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(new PipelineSummaryDto([], 0m, 0m, 0, 0.0));

        public Task<IReadOnlyList<OpportunityStageHistoryDto>> GetStageHistoryAsync(Guid tenantId, Guid opportunityId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OpportunityStageHistoryDto>>([]);

        public Task<OpportunityDto> TransitionStageAsync(Guid tenantId, Guid actorId, string requestId, Guid opportunityId, TransitionStageCommand command, CancellationToken ct)
        {
            LastTransitionOppId = opportunityId;
            LastTransitionCommand = command;
            return Task.FromResult(new OpportunityDto(
                opportunityId, "OPP-001", "Test Deal", null, command.TargetStage, 10_000m, "USD", 50, null, null, DateTimeOffset.UtcNow, 2));
        }

        public Task<ConvertedLeadResultDto> ConvertLeadAtomicAsync(Guid tenantId, Guid actorId, string requestId, Guid leadId, ConvertLeadCommand command, CancellationToken ct)
        {
            LastConvertCommand = command;
            return Task.FromResult(new ConvertedLeadResultDto(leadId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));
        }
    }
}

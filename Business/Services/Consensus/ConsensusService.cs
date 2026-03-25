using System.Text.Json;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Consensus;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Data;
using DataLabelProject.Data.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Consensus;

public class ConsensusService : IConsensusService
{
	private readonly IConsensusRepository _consensusRepository;
	private readonly ILabelingTaskItemRepository _taskRepository;
	private readonly AppDbContext _context;
	private readonly IActivityLogService _activityLog;

	public ConsensusService(
		IConsensusRepository consensusRepository,
		ILabelingTaskItemRepository taskRepository,
		AppDbContext context,
		IActivityLogService activityLog)
	{
		_consensusRepository = consensusRepository;
		_taskRepository = taskRepository;
		_context = context;
		_activityLog = activityLog;
	}

	public async Task<ConsensusResponse> CreateConsensusAsync(ConsensusCreateRequest request)
	{
		var datasetItem = await _context.DatasetItems
			.Include(di => di.ItemDataset)
			.FirstOrDefaultAsync(di => di.DatasetItemId == request.DatasetItemId)
			?? throw new KeyNotFoundException("Dataset item not found");

		var payloadJson = request.Payload.GetRawText();

		var consensus = new Business.Models.Consensus
		{
			ConsensusId = Guid.NewGuid(),
			DatasetItemId = request.DatasetItemId,
			Payload = payloadJson,
			CreatedAt = DateTime.UtcNow
		};

		var created = await _consensusRepository.CreateAsync(consensus);

		if (datasetItem.ItemDataset?.ProjectId is Guid projectId)
		{
			await _activityLog.LogAsync(
				projectId, null, "CONSENSUS_CREATED", "Consensus",
				consensus.ConsensusId, new { datasetItemId = request.DatasetItemId });
		}

		return MapToDto(created);
	}

	public async Task<ConsensusResponse?> GetConsensusByIdAsync(Guid consensusId)
	{
		var consensus = await _consensusRepository.GetByIdAsync(consensusId);
		return consensus == null ? null : MapToDto(consensus);
	}

	public async Task<ConsensusResponse?> GetConsensusByTaskItemIdAsync(Guid taskItemId)
	{
		var taskItem = await _taskRepository.GetByIdAsync(taskItemId);
		if (taskItem == null)
			return null;

		var consensuses = await _consensusRepository.GetByDatasetItemIdAsync(taskItem.DatasetItemId);
		var consensus = consensuses.FirstOrDefault();

		return consensus == null ? null : MapToDto(consensus);
	}

	public async Task<PagedResponse<ConsensusResponse>> GetConsensusesAsync(ConsensusQueryParameters @params)
	{
		var paged = await _consensusRepository.GetConsensusesAsync(@params);

		return new PagedResponse<ConsensusResponse>
		{
			Items = paged.Items.Select(MapToDto).ToList(),
			TotalItems = paged.TotalItems,
			Page = @params.Page,
			PageSize = @params.PageSize,
		};
	}

	private static ConsensusResponse MapToDto(Business.Models.Consensus consensus)
	{
		object parsedPayload;
		double agreementScore = 0;
		try
		{
			var jsonDoc = JsonDocument.Parse(consensus.Payload);
			var root = jsonDoc.RootElement;
			if (root.TryGetProperty("originalPayload", out var originalElement))
			{
				parsedPayload = JsonSerializer.Deserialize<object>(originalElement.GetString() ?? "{}") ?? "{}";
			}
			else
			{
				parsedPayload = JsonSerializer.Deserialize<object>(consensus.Payload) ?? consensus.Payload;
			}
			if (root.TryGetProperty("agreementScore", out var scoreElement) && scoreElement.TryGetDouble(out var score))
			{
				agreementScore = score;
			}
		}
		catch
		{
			parsedPayload = consensus.Payload;
		}

		return new ConsensusResponse
		{
			ConsensusId = consensus.ConsensusId,
			DatasetItemId = consensus.DatasetItemId,
			Payload = parsedPayload,
			CreatedAt = consensus.CreatedAt,
			Result = consensus.Review?.Result
		};
	}
}

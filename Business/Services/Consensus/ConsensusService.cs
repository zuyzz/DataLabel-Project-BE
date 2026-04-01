using System.Text.Json;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Consensus;
using DataLabelProject.Business.Services.ActivityLogs.Constant;
using DataLabelProject.Business.Models.Enums;
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
		var taskItem = await _context.LabelingTaskItems
			.Include(ti => ti.DatasetItem)
				.ThenInclude(di => di.ItemDataset)
			.FirstOrDefaultAsync(ti => ti.TaskItemId == request.TaskItemId)
			?? throw new KeyNotFoundException("Task item not found.");

		var datasetItemId = taskItem.DatasetItemId;
		var payloadJson = request.Payload.GetRawText();

		var consensus = new Business.Models.Consensus
		{
			ConsensusId = Guid.NewGuid(),
			DatasetItemId = datasetItemId,
			Payload = payloadJson,
			CreatedAt = DateTime.UtcNow
		};

		var created = await _consensusRepository.CreateAsync(consensus);

		taskItem.Status = LabelingTaskItemStatus.Completed;
		await _taskRepository.SaveChangesAsync();

		if (taskItem.DatasetItem?.ItemDataset?.ProjectId is Guid projectId)
		{
			await _activityLog.LogAsync(
				projectId, null, ActivityEvents.ConsensusCreated, ActivityTargets.Consensus,
				consensus.ConsensusId, new ConsensusCreatedDetails { DatasetItemId = datasetItemId });
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

using DataLabelProject.Application.DTOs.Annotations;
using DataLabelProject.Application.DTOs.Common;
using DataLabelProject.Application.DTOs.Consensus;
using DataLabelProject.Application.DTOs.Reviews;
using DataLabelProject.Application.DTOs.Tasks;
using DataLabelProject.Business.Services.ActivityLogs.Constant;
using DataLabelProject.Business.Models;
using DataLabelProject.Business.Models.Enums;
using DataLabelProject.Business.Services.Users;
using DataLabelProject.Business.Services.ActivityLogs;
using DataLabelProject.Data.Repositories.Abstractions;
using DataLabelProject.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DataLabelProject.Business.Services.Reviews
{
    public class ReviewService : IReviewService
    {
        private readonly IReviewRepository _reviewRepository;
        private readonly ILabelingTaskItemRepository _taskItemRepository;
        private readonly IConsensusRepository _consensusRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityLogService _activityLog;

        public ReviewService(
            IReviewRepository reviewRepository,
            ILabelingTaskItemRepository taskItemRepository,
            IConsensusRepository consensusRepository,
            ICurrentUserService currentUserService,
            IActivityLogService activityLog)
        {
            _reviewRepository = reviewRepository;
            _taskItemRepository = taskItemRepository;
            _consensusRepository = consensusRepository;
            _currentUserService = currentUserService;
            _activityLog = activityLog;
        }

        public async Task<PagedResponse<ReviewResponse>> GetReviewsAsync(
            ReviewQueryParameters @params)
        {
            IQueryable<Review> query = _reviewRepository.Query()
                .OrderByDescending(r => r.ReviewedAt)
                .Include(r => r.ReviewTaskItem)
                .Include(r => r.ReviewConsensus);

            query = ApplyUserFilter(query);
            query = ApplyParamFilters(query, @params);

            return await query.ToPagedResponseAsync(@params, MapToResponse);
        }

        public async Task<PagedResponse<TaskItemReviewsResponse>> GetReviewsByTaskAsync(
            Guid taskId, 
            ReviewQueryParameters @params)
        {
            IQueryable<LabelingTaskItem> query = _taskItemRepository.Query()
                .Where(ti => ti.TaskId == taskId)
                .Include(ti => ti.Reviews)
                    .ThenInclude(r => r.ReviewConsensus);

            query = ApplyUserFilter(query);
            query = ApplyParamFilters(query, @params);

            return await query.ToPagedResponseAsync(@params, MapToResponse);
        }

        public async Task<PagedResponse<ReviewResponse>> GetReviewsByTaskItemAsync(
            Guid taskItemId, 
            ReviewQueryParameters @params)
        {
            IQueryable<Review> query = _reviewRepository.Query()
                .Where(r => r.TaskItemId == taskItemId)
                .OrderByDescending(r => r.ReviewedAt)
                .Include(r => r.ReviewTaskItem)
                .Include(r => r.ReviewConsensus);

            query = ApplyUserFilter(query);
            query = ApplyParamFilters(query, @params);

            return await query.ToPagedResponseAsync(@params, MapToResponse);
        }

        private IQueryable<Review> ApplyUserFilter(
            IQueryable<Review> query)
        {
            var currentUserId = _currentUserService.UserId;
            var currentUserRoles = _currentUserService.Roles;

            if (currentUserRoles.Contains("reviewer") && currentUserId.HasValue)
            {
                query = query.Where(r => r.ReviewerId == currentUserId.Value);
            }

            return query;
        }

        private IQueryable<LabelingTaskItem> ApplyUserFilter(
            IQueryable<LabelingTaskItem> query)
        {
            var currentUserId = _currentUserService.UserId;
            var roles = _currentUserService.Roles;

            if (roles.Contains("reviewer") && currentUserId.HasValue)
            {
                query = query.Where(ti =>
                    ti.Reviews.Any(r => r.ReviewerId == currentUserId.Value));
            }

            return query;
        }

        private IQueryable<Review> ApplyParamFilters(
            IQueryable<Review> query,
            ReviewQueryParameters @params)
        {
            if (@params.Result.HasValue)
            {
                query = query.Where(r => r.Result == @params.Result.Value);
            }

            return query;
        }

        private IQueryable<LabelingTaskItem> ApplyParamFilters(
            IQueryable<LabelingTaskItem> query,
            ReviewQueryParameters @params)
        {
            if (@params.Result.HasValue)
            {
                query = query.Where(ti =>
                    ti.Reviews.Any(r => r.Result == @params.Result.Value));
            }

            return query;
        }

        public async Task<ReviewResponse> CreateReviewAsync(CreateReviewRequest request)
        {
            var taskItem = await _taskItemRepository.GetByIdAsync(request.TaskItemId) 
                ?? throw new KeyNotFoundException("Task not found");

            var consensus = await _consensusRepository.GetByIdAsync(request.ConsensusId)
                ?? throw new KeyNotFoundException("Consensus not found");

            switch (taskItem.Status)
            {
                case LabelingTaskItemStatus.Completed:
                case LabelingTaskItemStatus.Incompleted:
                    throw new InvalidOperationException("This item have already been reviewed");
            }

            var reviewerId = _currentUserService.UserId!.Value;

            var review = new Review
            {
                ReviewId = Guid.NewGuid(),
                ConsensusId = consensus.ConsensusId,
                TaskItemId = taskItem.TaskItemId,
                ReviewerId = reviewerId,
                Result = request.Result,
                Feedback = request.Feedback,
                ReviewedAt = DateTime.UtcNow,
            };

            await _reviewRepository.CreateAsync(review);

            // can using event handler here
            switch (review.Result)
            {
                case ReviewResult.Approved:
                    taskItem.Status = LabelingTaskItemStatus.Completed;
                    break;
                case ReviewResult.Rejected:
                    taskItem.Status = LabelingTaskItemStatus.Incompleted;
                    break;
            }

            await _reviewRepository.SaveChangesAsync();
            await _taskItemRepository.SaveChangesAsync();

            await _activityLog.LogAsync(taskItem.ProjectId, reviewerId, ActivityEvents.ReviewSubmitted, ActivityTargets.Review, review.ReviewId, new ReviewSubmittedDetails
            {
                TaskItemId = taskItem.TaskItemId,
                Result = review.Result.ToString()
            });

            return MapToResponse(review);
        }

        private ReviewResponse MapToResponse(Review review)
        {
            return new ReviewResponse
            {
                ReviewId = review.ReviewId,
                ReviewerId = review.ReviewerId,
                Result = review.Result,
                Feedback = review.Feedback,
                ReviewedAt = review.ReviewedAt,
                Consensus = new ConsensusResponse
                {
                    ConsensusId = review.ReviewConsensus.ConsensusId,
                    DatasetItemId = review.ReviewConsensus.DatasetItemId,
                    Payload = review.ReviewConsensus.Payload,
                    CreatedAt = review.ReviewConsensus.CreatedAt
                }
            };
        }

        private TaskItemReviewsResponse MapToResponse(LabelingTaskItem tItem)
        {
            return new TaskItemReviewsResponse
            {
                TaskItemId = tItem.TaskItemId,
                DatasetItemId = tItem.DatasetItemId,
                Status = tItem.Status,
                Reviews = tItem.Reviews
                    .Select(MapToResponse)
                    .ToList()
            };
        }
    }
}
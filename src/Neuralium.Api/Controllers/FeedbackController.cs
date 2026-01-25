using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neuralium.Api.Models;
using Neuralium.Data;
using Neuralium.Data.Models;

namespace Neuralium.Api.Controllers;

/// <summary>
/// Endpoints for submitting user feedback on news items and keywords.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public partial class FeedbackController(
    NeuraliumDbContext dbContext,
    ILogger<FeedbackController> logger) : ControllerBase
{
    /// <summary>
    /// Submit feedback on a news item (thumbs up/down).
    /// </summary>
    /// <param name="id">News item ID</param>
    /// <param name="request">Feedback data (userId, feedbackType)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>201 Created if new feedback, 200 OK if updated existing, 404 if item not found</returns>
    [HttpPost("newsitem/{id}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitNewsItemFeedback(
        int id,
        [FromBody] SubmitFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        // Verify news item exists
        var itemExists = await dbContext.NewsItems.AnyAsync(i => i.Id == id, cancellationToken);
        if (!itemExists)
        {
            return NotFound(new { Error = $"News item with ID {id} not found" });
        }

        // Check if user already provided feedback
        var existingFeedback = await dbContext.NewsItemFeedbacks
            .FirstOrDefaultAsync(f => f.NewsItemId == id && f.UserId == request.UserId, cancellationToken);

        if (existingFeedback is not null)
        {
            // Update existing feedback
            existingFeedback.FeedbackType = request.FeedbackType;
            existingFeedback.CreatedAtUtc = DateTime.UtcNow; // Update timestamp
            await dbContext.SaveChangesAsync(cancellationToken);

            LogNewsItemFeedbackUpdated(id, request.UserId, request.FeedbackType);

            return Ok(new { Message = "Feedback updated", ItemId = id, UserId = request.UserId });
        }

        // Create new feedback
        var feedback = new NewsItemFeedback
        {
            NewsItemId = id,
            UserId = request.UserId,
            FeedbackType = request.FeedbackType,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.NewsItemFeedbacks.Add(feedback);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogNewsItemFeedbackCreated(id, request.UserId, request.FeedbackType);

        return CreatedAtAction(nameof(SubmitNewsItemFeedback), new { id }, new { Message = "Feedback created", ItemId = id, UserId = request.UserId });
    }

    /// <summary>
    /// Submit feedback on a classification keyword (thumbs up/down).
    /// </summary>
    /// <param name="id">Keyword ID</param>
    /// <param name="request">Feedback data (userId, feedbackType)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>201 Created if new feedback, 200 OK if updated existing, 404 if keyword not found</returns>
    [HttpPost("keyword/{id}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitKeywordFeedback(
        int id,
        [FromBody] SubmitFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        // Verify keyword exists
        var keywordExists = await dbContext.TopicKeywords.AnyAsync(k => k.Id == id, cancellationToken);
        if (!keywordExists)
        {
            return NotFound(new { Error = $"Keyword with ID {id} not found" });
        }

        // Check if user already provided feedback
        var existingFeedback = await dbContext.KeywordFeedbacks
            .FirstOrDefaultAsync(f => f.TopicKeywordId == id && f.UserId == request.UserId, cancellationToken);

        if (existingFeedback is not null)
        {
            // Update existing feedback
            existingFeedback.FeedbackType = request.FeedbackType;
            existingFeedback.CreatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            LogKeywordFeedbackUpdated(id, request.UserId, request.FeedbackType);

            return Ok(new { Message = "Feedback updated", KeywordId = id, UserId = request.UserId });
        }

        // Create new feedback
        var feedback = new KeywordFeedback
        {
            TopicKeywordId = id,
            UserId = request.UserId,
            FeedbackType = request.FeedbackType,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.KeywordFeedbacks.Add(feedback);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogKeywordFeedbackCreated(id, request.UserId, request.FeedbackType);

        return CreatedAtAction(nameof(SubmitKeywordFeedback), new { id }, new { Message = "Feedback created", KeywordId = id, UserId = request.UserId });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated feedback for news item {ItemId} by user {UserId}: {FeedbackType}")]
    private partial void LogNewsItemFeedbackUpdated(int itemId, string userId, string feedbackType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created feedback for news item {ItemId} by user {UserId}: {FeedbackType}")]
    private partial void LogNewsItemFeedbackCreated(int itemId, string userId, string feedbackType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated feedback for keyword {KeywordId} by user {UserId}: {FeedbackType}")]
    private partial void LogKeywordFeedbackUpdated(int keywordId, string userId, string feedbackType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created feedback for keyword {KeywordId} by user {UserId}: {FeedbackType}")]
    private partial void LogKeywordFeedbackCreated(int keywordId, string userId, string feedbackType);
}

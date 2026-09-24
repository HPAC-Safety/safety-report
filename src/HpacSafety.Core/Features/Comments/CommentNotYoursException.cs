namespace HpacSafety.Core.Features.Comments;

/// <summary>Only a comment's author may edit or delete it (ADR-0114).</summary>
public sealed class CommentNotYoursException()
	: Exception("Only the member who wrote a comment may change it.");

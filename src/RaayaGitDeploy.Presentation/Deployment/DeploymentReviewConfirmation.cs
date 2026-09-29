using System.Text;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

/// <summary>
/// Produces the confirmation content shown immediately before a reviewed deployment.
/// Keeps transport details and credentials out of the review surface while making
/// destructive remote mutations explicit.
/// </summary>
public sealed record DeploymentReviewConfirmation(
    string Title,
    string Summary,
    string Detail,
    bool IsDestructive,
    string PrimaryActionText)
{
    public static DeploymentReviewConfirmation From(DeploymentReviewSnapshot review)
    {
        ArgumentNullException.ThrowIfNull(review);

        var summary = $"{review.UploadCount} upload(s), {review.DeleteCount} delete(s) → {review.ServerProfileName} ({review.RemoteRoot})";
        var detail = new StringBuilder()
            .AppendLine($"Target: {review.ServerProfileName}")
            .AppendLine($"Remote root: {review.RemoteRoot}")
            .AppendLine($"Uploads: {review.UploadCount}")
            .AppendLine($"Deletes: {review.DeleteCount}");

        if (review.RequiresExplicitConfirmation)
        {
            detail.AppendLine().AppendLine("Remote paths that will be deleted:");
            foreach (var path in review.DestructiveRemotePaths)
                detail.Append("• ").AppendLine(path);
        }

        return new DeploymentReviewConfirmation(
            review.RequiresExplicitConfirmation ? "Confirm destructive deployment" : "Confirm deployment",
            summary,
            detail.ToString().TrimEnd(),
            review.RequiresExplicitConfirmation,
            review.RequiresExplicitConfirmation ? "Deploy and delete" : "Deploy");
    }
}

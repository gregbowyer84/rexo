namespace Rexo.Ci;

using Rexo.Core.Models;

public static class CiDetector
{
    public static CiInfo Detect()
    {
        // GitHub Actions
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            return new CiInfo(
                IsCi: true,
                Provider: "github-actions",
                BuildId: Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
                Branch: Environment.GetEnvironmentVariable("GITHUB_REF_NAME"),
                IsPullRequest: Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") == "pull_request");
        }

        // Azure DevOps
        if (Environment.GetEnvironmentVariable("TF_BUILD") == "True")
        {
            return new CiInfo(
                IsCi: true,
                Provider: "azure-devops",
                BuildId: Environment.GetEnvironmentVariable("BUILD_BUILDID"),
                Branch: Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH"),
                IsPullRequest: Environment.GetEnvironmentVariable("BUILD_REASON") == "PullRequest");
        }

        // GitLab CI
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITLAB_CI")))
        {
            return new CiInfo(
                IsCi: true,
                Provider: "gitlab-ci",
                BuildId: Environment.GetEnvironmentVariable("CI_PIPELINE_ID"),
                Branch: Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME"),
                IsPullRequest: !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_MERGE_REQUEST_ID")));
        }

        // Bitbucket Pipelines
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BITBUCKET_BUILD_NUMBER")))
        {
            return new CiInfo(
                IsCi: true,
                Provider: "bitbucket-pipelines",
                BuildId: Environment.GetEnvironmentVariable("BITBUCKET_BUILD_NUMBER"),
                Branch: Environment.GetEnvironmentVariable("BITBUCKET_BRANCH"),
                IsPullRequest: !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BITBUCKET_PR_ID")));
        }

        // Generic CI
        if (Environment.GetEnvironmentVariable("CI") == "true")
        {
            return new CiInfo(
                IsCi: true,
                Provider: "unknown",
                BuildId: null,
                Branch: null,
                IsPullRequest: false);
        }

        return new CiInfo(
            IsCi: false,
            Provider: null,
            BuildId: null,
            Branch: null,
            IsPullRequest: false);
    }
}

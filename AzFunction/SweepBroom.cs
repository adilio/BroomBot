using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.Linq;

namespace BroomBot
{
    public static class SweepBroom
    {
        [FunctionName("SweepBroom")]
        public static async void Run([TimerTrigger("0 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            if (myTimer.IsPastDue)
            {
                log.LogInformation("Behind schedule");
            }

            log.LogInformation($"Started sweep: {DateTime.Now}");

            // Get the environment vars
            string PAT = Environment.GetEnvironmentVariable("PAT", EnvironmentVariableTarget.Process);
            string organization = Environment.GetEnvironmentVariable("Organization", EnvironmentVariableTarget.Process);
            string project = Environment.GetEnvironmentVariable("Project", EnvironmentVariableTarget.Process);
            string broomBotName = Environment.GetEnvironmentVariable("ADOAccountName", EnvironmentVariableTarget.Process);
            int staleAge = Convert.ToInt32(Environment.GetEnvironmentVariable("StaleAge", EnvironmentVariableTarget.Process));

            string collectionUri = $"https://dev.azure.com/{organization}";
            VssCredentials creds = new VssBasicCredential(string.Empty, PAT);

            // Connect to Azure DevOps Services
            VssConnection connection = new VssConnection(new Uri(collectionUri), creds);

            // Get a GitHttpClient to talk to the Git endpoints
            using (GitHttpClient gitClient = connection.GetClient<GitHttpClient>())
            {
                // Get all the PRs and filter out the ones that were created since the stale date
                IList<GitPullRequest> allPRs = await BroomBotUtils.GetPullRequests(gitClient, project);

                if (allPRs.Count == 0)
                {
                    log.LogInformation($"Found no PRs in project {project}");
                    return;
                }

                DateTime staleDate = DateTime.Now.AddHours(-staleAge);
                IList<GitPullRequest> createdBeforeStaleDate = allPRs.Where(p => p.CreationDate < staleDate).ToList();

                if (createdBeforeStaleDate.Count == 0)
                {
                    log.LogInformation($"Found no PRs created before {staleDate}");
                    return;
                }

                // which PRs haven't had a comment since staledate
                IList<GitPullRequest> stalePRs = await BroomBotUtils.CheckPullRequestFreshness(
                    gitClient, project, createdBeforeStaleDate, staleDate);

                if (stalePRs.Count == 0)
                {
                    log.LogInformation($"Found no stale PRs before {staleDate}");
                    return;
                }

                // PRs that are stale whose last comment is from broombot

                // PRs that need to be abandoned
            }

            log.LogInformation($"Finished sweep: {DateTime.Now}");
        }
    }
}

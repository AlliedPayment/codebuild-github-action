using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CodeBuild;
using Amazon.CodeBuild.Model;

namespace Allied.Codebuild
{
    class Program
    {
        static async Task Main(string[] args)
        {

            //System.Environment.SetEnvironmentVariable("INPUT_PROJECT-NAME", "BuildContainers_Batch");
            //System.Environment.SetEnvironmentVariable("INPUT_BUILDSPEC-OVERRIDE", "");
            //System.Environment.SetEnvironmentVariable("INPUT_ENV-VARS-FOR-CODEBUILD", "Version, BUILD_SHA, BUILD_REPOSITORY, BUILD_REPOSITORY_OWNER,BUILD_BRANCH,BUILD_REF");
            //System.Environment.SetEnvironmentVariable("INPUT_WAITTIMEOUT", "00:20:00");
            //System.Environment.SetEnvironmentVariable("INPUT_COMMAND", "wait");
            //System.Environment.SetEnvironmentVariable("INPUT_ARN", "arn:aws:codebuild:us-east-1:547578534168:build-batch/BuildContainers_Batch:72d4e228-5fe3-42e0-8f6e-e14ab4213395");
            //System.Environment.SetEnvironmentVariable("Version", "0.109.23856.10");
            //System.Environment.SetEnvironmentVariable("BUILD_SHA", "521646a1a1472f6e1c9b56691e52b9c736548cef");
            //System.Environment.SetEnvironmentVariable("BUILD_REPOSITORY", "BillPay");
            //System.Environment.SetEnvironmentVariable("BUILD_REPOSITORY_OWNER", "AlliedPayment");
            //System.Environment.SetEnvironmentVariable("BUILD_BRANCH", "ABP-20375-crossroadsfcu-sharetec-fix");
            //System.Environment.SetEnvironmentVariable("BUILD_REF", "");
            
            var inputs = GetInputs();
            var command = inputs.GetValue("Command", "wait");
            var req = GetRequest(inputs);

            if (command == "build")
            {
                Console.WriteLine("Building");
                var arn = await Build(req);
                Console.WriteLine("::set-output name=aws-build-id::{0}", arn);
                Console.WriteLine("Build-Id: " + arn);
            }

            if (command == "wait")
            {
                Console.WriteLine("Waiting");
                var status = await Wait(req.Arn, req.WaitTimeout);
                Console.WriteLine("::set-output name=build-status::{0}", status);
                Console.WriteLine("Build-Status: " + status);
                if (status != StatusType.SUCCEEDED)
                {
                    Environment.ExitCode = 2;
                }
            }
        }

        static BuildRequest GetRequest(Dictionary<string, string> inputs)
        {
            var req = new BuildRequest();
            req.ProjectName = inputs.GetValue("PROJECT-NAME");
            req.BuildspecOverride= inputs.GetValue("BUILDSPEC-OVERRIDE");
            req.Arn = inputs.GetValue("ARN", "");
            req.WaitTimeout = TimeSpan.Parse(inputs.GetValue("WAITTIMEOUT", "00:15:00"));
            req.Command = inputs.GetValue("COMMAND", "build");
            var environmentVariables = inputs.GetValue("ENV-VARS-FOR-CODEBUILD", "")
                .Split(',')
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x.Trim())
                ;
            req.EnvironmentVariablesOverride = new List<EnvironmentVariable>();
            foreach (var env in environmentVariables)
            {
                if (!string.IsNullOrEmpty(env) && !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(env)))
                {
                    req.EnvironmentVariablesOverride.Add(new EnvironmentVariable()
                    {
                        Value = System.Environment.GetEnvironmentVariable(env),
                        Type = EnvironmentVariableType.PLAINTEXT,
                        Name = env
                    });
                }
            }
            return req;
        }
      
        static async Task<string> Wait(string arn, TimeSpan timeout, int maxRetries = 3)
        {
            var status = StatusType.IN_PROGRESS;
            var client = new Amazon.CodeBuild.AmazonCodeBuildClient();
            var waitTime = TimeSpan.Parse("00:00:10");
            bool isComplete = false;
            bool isTimedOut = false;
            bool isSuccessful = false;
            DateTimeOffset start = DateTimeOffset.Now;
            int retry = 0;
            while (!isComplete && !isTimedOut)
            {
                var result = await client.BatchGetBuildBatchesAsync(new BatchGetBuildBatchesRequest()
                {
                    Ids = new List<string>() {  arn }
                });
                isComplete = result.BuildBatches.All(x => x.Complete);
                isSuccessful = result.BuildBatches.All(x => x.BuildBatchStatus == StatusType.SUCCEEDED);
                status = result.BuildBatches.FirstOrDefault()?.BuildBatchStatus ?? StatusType.IN_PROGRESS;
                isTimedOut = (DateTimeOffset.Now >= start.Add(timeout));
                if (!isComplete)
                {
                    Console.WriteLine("Not complete... waiting " + waitTime);
                    await Task.Delay(waitTime);
                }

                if (isComplete && !isSuccessful && !isTimedOut && retry < maxRetries)
                {
                    //retry 
                    retry += 1;
                    var retryResult = await  client.RetryBuildBatchAsync(new RetryBuildBatchRequest()
                    {
                        Id = arn,
                        RetryType = RetryBuildBatchType.RETRY_FAILED_BUILDS,
                        IdempotencyToken = Guid.NewGuid().ToString(),
                    });
                    isComplete = false;
                    start = DateTimeOffset.UtcNow;
                    arn = retryResult.BuildBatch.Arn;
                }
            }

            if (isTimedOut)
            {
                status = StatusType.TIMED_OUT;
                Console.WriteLine("::error::Timed out");
                Console.WriteLine("Timed out");
            }

            if (!isComplete)
            {
                Console.WriteLine("Complete");
                Console.WriteLine("::debug::Complete");
                Console.WriteLine("Successful: " + isSuccessful);
            }
            Console.WriteLine("::debug::Status " + status);
            return status;
        }

        static async Task<string> Build(BuildRequest request)
        {
            var client = new Amazon.CodeBuild.AmazonCodeBuildClient();
            var result = await client.StartBuildBatchAsync(new StartBuildBatchRequest()
            {
                EnvironmentVariablesOverride = request.EnvironmentVariablesOverride,
                ProjectName = request.ProjectName,
                BuildspecOverride = request.BuildspecOverride,
            });
            var arn = result.BuildBatch.Arn;
            return arn;
        }

        static Dictionary<string, string> GetInputs()
        {
            IDictionary vars = System.Environment.GetEnvironmentVariables();
            var results = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string kvp in vars.Keys.OfType<string>().Where(x => x.StartsWith("INPUT_")))
            {
                results[kvp.Replace("INPUT_","")] = (string)vars[kvp];
            }
            return results;
        }

    }

    public static class DictionaryExtensions
    {
        public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV))
        {
            TV value;
            return dict.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}

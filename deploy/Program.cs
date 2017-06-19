using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace OnceAndFuture.Deployment
{
    static class Configuration
    {
        public const string Application = "onceandfuture";
        public const string BaseBucketName = "base-storagebucket-1p0p3r2s2844b";
        public const string GithubKey =
            "AQECAHhWoJ/zvEW/Cnd+TqAgiBGGdWt7YvQjFvMQyDZb5ZEVfQAAAUkwggFFBgkqhkiG9w0BBwagggE2MIIBMgIBADCCASsGCSqGSIb3" +
            "DQEHATAeBglghkgBZQMEAS4wEQQMYsZ3PIAMxG1v6FoiAgEQgIH9ArcnZCrFM5ERDzUBO1U3e+p33H9jxe1UTfTEMoXwf+iq4QO6HJH8" +
            "pl2uG0AxK7qPGnAtGa/fqyWzM8mKmfbchhGk2EeY2ds4FBsjDDqGL4zpS4Qx62zvgQpRdMyVbJ/r8KFG1lm2to+6zZjtE6byfCoKA2zM" +
            "auj+j7Xc7sGsXY9V926Xwn1tOV7mC3svSPIgJ+UwEhyE2MRSoBm0M3OvQdtPzbwwwdLLalJhhpNQHjqvLQHEw4O4dDoQGDIwyGEQ2RNg" +
            "CTFTRLBsZXhmswyEiONWc4JV/BfHEmPNDpRKJPsDRyuppJBrveVs+lcP5PODbB2TXMwR42FKnByKiA==";
        public const string GitUrl = "git@github.com:DeCarabas/onceandfuture.git";
        public const string Port = "8080";
    }

    class BuildVersion
    {
        public string BuildDate;
        public string BuildTime;
        public string Commit;

        public string Release => Commit.Substring(0, 7);

        public override string ToString()
        {
            return String.Format("{0}/{1}/{2}", BuildDate, BuildTime, Commit);
        }
    }

    class Program
    {
        static readonly RegionEndpoint region = RegionEndpoint.USWest2;
        static readonly AmazonCloudFormationClient cloudFormationClient = new AmazonCloudFormationClient(region);
        static readonly AmazonAutoScalingClient autoScalingClient = new AmazonAutoScalingClient(region);
        static readonly AmazonElasticLoadBalancingV2Client elbClient = new AmazonElasticLoadBalancingV2Client(region);
        static readonly AmazonS3Client s3client = new AmazonS3Client(RegionEndpoint.USWest2);

        static ProgramOpts Options = new ProgramOpts()
            .AddOption("help", "Display this help.", o => o.Flag('?'))
            .AddVerb("build", "Do a build", DoBuild, v => v
                .AddOption("commit", "The commit to build.", o => o.AcceptValue())
                .AddOption("force", "Rebuild even if we've already built the specified commit.")
            )
            .AddVerb("deploy", "Deploy to an environment.", DoDeploy, v => v
            )
            ;

        static string CreateStack(DateTime startTime, BuildVersion build, StackBase stack)
        {
            Console.WriteLine("Creating {0} stack for build {1}", stack.StackType, build);
            string stackName = String.Join("-", new string[] {
                stack.Environment,
                Configuration.Application,
                "doty",
                build.Release,
                startTime.Year.ToString(),
                startTime.Month.ToString(),
                startTime.Day.ToString(),
                startTime.Hour.ToString(),
                startTime.Minute.ToString(),
                startTime.Second.ToString(),
            });

            //string template = CreateBuildTemplate(stackName, commit);
            //string outFile = String.Format("debug-{0}-{1}-template.json", stack.StackType, stack.Environment);
            //File.WriteAllText(outFile, template);

            Console.WriteLine("    Creating stack {0}", stackName);

            CreateStackResponse response = cloudFormationClient.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                StackName = stackName,
                Parameters = stack.Parameters,
                TemplateBody = stack.GetTemplate(stackName, build),
                TimeoutInMinutes = 20,
                Tags = stack.GetTags(build),
            }).Result;

            string stackId = response.StackId;
            Console.WriteLine("Created {0}", stackId);
            return stackId;
        }

        static void SleepWithAnimation(TimeSpan sleepDuration)
        {
            const int fps = 4;
            const string anim = @"/-\|";
            TimeSpan duration = TimeSpan.FromSeconds(1.0 / fps);

            int frame = 0;
            TimeSpan slept = TimeSpan.Zero;
            while (slept < sleepDuration)
            {
                Console.Write("{0}\r", anim[frame]);
                frame = (frame + 1) % anim.Length;
                Thread.Sleep(duration);
                slept += duration;
            }
        }

        static bool WaitForStackCreated(string stackId)
        {
            Console.WriteLine("Waiting for doneness of {0}...", stackId);

            string lastId = null;
            bool? success = null;

            do
            {
                SleepWithAnimation(TimeSpan.FromSeconds(5));
                var response = cloudFormationClient.DescribeStackEventsAsync(new DescribeStackEventsRequest
                {
                    StackName = stackId,
                }).Result;
                if (response.StackEvents.Count == 0) { continue; }

                var newEvents = new List<StackEvent>();
                foreach (StackEvent evt in response.StackEvents)
                {
                    if (evt.EventId == lastId) { break; }
                    newEvents.Add(evt);
                    if (evt.PhysicalResourceId == stackId)
                    {
                        if (evt.ResourceStatus == ResourceStatus.CREATE_COMPLETE)
                        {
                            success = true;
                        }
                        else if (evt.ResourceStatus == ResourceStatus.DELETE_COMPLETE)
                        {
                            success = false;
                        }
                        else if (evt.ResourceStatus == ResourceStatus.CREATE_FAILED)
                        {
                            success = false;
                        }
                    }
                }
                newEvents.Reverse();
                foreach (StackEvent evt in newEvents)
                {
                    Console.WriteLine(
                        "{0,-20} {1,-20} {2,-40} {3} - {4}",
                        evt.Timestamp,
                        evt.ResourceStatus,
                        evt.ResourceType,
                        evt.LogicalResourceId,
                        evt.ResourceStatusReason
                    );
                }
                lastId = response.StackEvents[0].EventId;
            } while (success == null);

            Console.WriteLine("Stack creation {0}", success.Value ? "SUCCEEDED" : "FAILED");
            return success.Value;
        }

        static bool WaitForStackHealthy(string stackId)
        {
            Console.WriteLine("Getting scaling group from {0}...", stackId);
            DescribeStackResourceResponse response;
            response = cloudFormationClient.DescribeStackResourceAsync(new DescribeStackResourceRequest
            {
                StackName = stackId,
                LogicalResourceId = "AutoScalingGroup",
            }).Result;
            string autoScalingGroupId = response.StackResourceDetail.PhysicalResourceId;

            Console.WriteLine("Instances from scaling group {0}...", autoScalingGroupId);
            var response2 = autoScalingClient.DescribeAutoScalingGroupsAsync(new DescribeAutoScalingGroupsRequest
            {
                AutoScalingGroupNames = { autoScalingGroupId },
            }).Result;
            var scalingGroup = response2.AutoScalingGroups[0];
            var targetGroup = scalingGroup.TargetGroupARNs[0];
            var instances = scalingGroup.Instances;

            Console.WriteLine("Checking to make sure instances come up OK...");
            bool success = false;
            TimeSpan timeWaited = TimeSpan.Zero;
            TimeSpan timeout = TimeSpan.FromMinutes(5);
            TimeSpan sleepTime = TimeSpan.FromSeconds(5);
            while (!success && timeWaited <= timeout)
            {
                success = CheckInstancesHealthy(targetGroup, instances);
                if (!success)
                {
                    SleepWithAnimation(sleepTime);
                    timeWaited += sleepTime;
                }
            }
            Console.WriteLine("Instances {0}", success ? "SUCCEEDED" : "FAILED");
            return success;
        }

        static bool CheckInstancesHealthy(string targetGroup, List<Instance> instances)
        {
            var response3 = elbClient.DescribeTargetHealthAsync(new DescribeTargetHealthRequest
            {
                TargetGroupArn = targetGroup,
            }).Result;

            Dictionary<string, TargetHealthDescription> healths;
            healths = response3.TargetHealthDescriptions.ToDictionary(k => k.Target.Id);
            foreach (Instance instance in instances)
            {
                if (!healths.TryGetValue(instance.InstanceId, out TargetHealthDescription health))
                {
                    Console.WriteLine("{0} not in health yet...", instance.InstanceId);
                    return false;
                }

                if (health.TargetHealth.State != TargetHealthStateEnum.Healthy)
                {
                    Console.WriteLine(
                        "{0} in state {1} : {2}",
                        instance.InstanceId,
                        health.TargetHealth.State,
                        health.TargetHealth.Reason
                    );
                    return false;
                }
            }
            Console.WriteLine("All instances healthy!");
            return true;
        }

        static Stack[] GetStacksByEnvironmentAndType(string environment, string type)
            => cloudFormationClient.DescribeStacksAsync().Result.Stacks.Where(
                s => s.Tags.Any(tag => tag.Key == "environment" && tag.Value == environment)
            ).Where(
                s => s.Tags.Any(tag => tag.Key == "stack-type" && tag.Value == type)
            ).ToArray();

        static string[] GetOldReleaseStacks(string environment)
            => GetStacksByEnvironmentAndType(environment, "release").Select(st => st.StackId).ToArray();

        static void DeleteStack(string stackId)
        {
            Console.WriteLine("Marking {0} for deletion...", stackId);
            cloudFormationClient.DeleteStackAsync(new DeleteStackRequest { StackName = stackId }).Wait();
        }

        static List<Dictionary<string, object>> LoadSecrets(string environment)
        {
            return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
                File.ReadAllText("secrets.json")
            )
            .Where(
                d => (string)(d["env"]) == environment || (string)(d["env"]) == "all"
            )
            .Select(
                d => new Dictionary<string, object> { { "KEY", d["name"] }, { "VALUE", d["value"] } }
            ).ToList();
        }

        static BuildVersion GetLastBuild()
        {
            List<S3Object> allObjects = new List<S3Object>();
            string nextMarker = null;
            do
            {
                var response = s3client.ListObjectsAsync(new ListObjectsRequest
                {
                    BucketName = Configuration.BaseBucketName,
                    Marker = nextMarker,
                    Prefix = "artifacts/onceandfuture",
                }).Result;
                allObjects.AddRange(response.S3Objects);
                nextMarker = response.IsTruncated ? response.NextMarker : null;
            } while (nextMarker != null);

            if (allObjects.Count == 0) { return null; }

            // Compare keys in reverse so largest key is first.
            allObjects.Sort((x, y) => String.Compare(y.Key, x.Key));
            S3Object newest = allObjects[0];

            var imageRegex = new Regex("artifacts/onceandfuture/([0-9]+)/([0-9]+Z)/([a-f0-9]+).tar.lz4");
            Match match = imageRegex.Match(newest.Key);
            return new BuildVersion
            {
                BuildDate = match.Groups[1].Value,
                BuildTime = match.Groups[2].Value,
                Commit = match.Groups[3].Value,
            };
        }

        static string GetPublishedCommit()
        {
            // git fetch origin master
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "log origin/master --format=format:%H --max-count=1",
                RedirectStandardOutput = true,
            });

            string commit = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            process.Dispose();

            return commit;
        }

        static int DoBuild(ParsedOpts args)
        {
            string commit = args["commit"].Value ?? GetPublishedCommit();
            Console.WriteLine("Building and uploading for commit {0}...", commit);

            DateTime startTime = DateTime.Now;

            if (!args["force"].Flag)
            {
                BuildVersion lastBuild = GetLastBuild();
                if (lastBuild.Commit == commit)
                {
                    Console.WriteLine("Latest build is for this commit, nothing to do.");
                    return 0;
                }

            }

            DateTime now = DateTime.UtcNow;
            string buildDate = String.Format("{0:D4}{1:D2}{2:D2}", now.Year, now.Month, now.Day);
            string buildTime = String.Format("{0:D2}{1:D2}{2:D2}Z", now.Hour, now.Minute, now.Second);
            var build = new BuildVersion { BuildDate = buildDate, BuildTime = buildTime, Commit = commit };

            Console.WriteLine("Building {0}", build);
            string stackId = CreateStack(startTime, build, new BuildStack());
            bool succeeded = WaitForStackCreated(stackId);
            DeleteStack(stackId);
            Console.WriteLine("BUILD {0}", succeeded ? "SUCCESS" : "FAILED");
            return succeeded ? 0 : 1;
        }

        static int DoDeploy(ParsedOpts args)
        {
            const string environment = "qa";

            DateTime startTime = DateTime.Now;
            BuildVersion build = GetLastBuild();

            Console.WriteLine("Deploying {0} to {1} @ {2}", build, environment, startTime);
            string[] oldStackIds = GetOldReleaseStacks(environment);
            if (oldStackIds.Length > 0)
            {
                Console.WriteLine("Existing stacks are:");
                for (int i = 0; i < oldStackIds.Length; i++)
                {
                    Console.WriteLine("    {0}", oldStackIds[i]);
                }
            }

            string stackId = CreateStack(startTime, build, new ReleaseStack(environment));
            bool succeeded = WaitForStackCreated(stackId) && WaitForStackHealthy(stackId);
            if (succeeded)
            {
                Console.WriteLine("CREATE SUCCESS");
                Console.WriteLine("Cleaning up old stacks...");
                for (int i = 0; i < oldStackIds.Length; i++) { DeleteStack(oldStackIds[i]); }
                Console.WriteLine("DEPLOY SUCCESS");
            }
            else
            {
                Console.WriteLine("FAILED -- Cleaning Up");
                DeleteStack(stackId);
            }

            return succeeded ? 0 : 1;
        }

        static int Main(string[] args)
        {
            try
            {
                ParsedOpts parsedArgs = Options.ParseArguments(args);
                if (parsedArgs.Error != null)
                {
                    Console.Error.WriteLine(parsedArgs.Error);
                    Console.Error.WriteLine(Options.GetHelp(parsedArgs.Verb));
                    return 1;
                }
                if (parsedArgs["help"].Flag)
                {
                    Console.WriteLine(Options.GetHelp(parsedArgs.Verb));
                    return 0;
                }

                return parsedArgs.Verb.Handler(parsedArgs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 99;
            }
        }
    }
}
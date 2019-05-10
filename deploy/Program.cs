using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.KeyManagementService;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public
        const string
            GithubKey =
                "AQICAHhihjs2/tSsp5GujMU1lz73/3FXzleZCliDs7++otppHwEjxRJlYVw" +
                "9jSsKI37kPEZbAAABSjCCAUYGCSqGSIb3DQEHBqCCATcwggEzAgEAMIIBLA" +
                "YJKoZIhvcNAQcBMB4GCWCGSAFlAwQBLjARBAxAFJT3uMWwMl6CgXsCARCAg" +
                "f7cNEDnWI1NPoBeH6nxMOMxhObKPL45Cm7ZECdheF+qoCifi6kuYoKigox0" +
                "H+Y0S1C0ddGTd1k8WkSNqcg/NMvtAc9odqcd+lmfDgwh7ClBiOasnGjdJAe" +
                "s6ROV9EiGuF/vPPlfeyFSO3ZRQ4jWIy/eGI9KPtTBdxqKFK2R+pGRsyb6h7" +
                "r79eL+718uemroX/0/HFYtPDy9dXo1Ho7Ce/9N095Gp22eUfa/9xjJwG2QX" +
                "qLTe3PpI8YUn+1Yy1PrM8mAJcxWXnOy1wLA/KPA4ZwClROqx53i5AyZPa/y" +
                "8rk4p3uvdeN49JLHk2/bDdHp1xcDTTn7UCrBm91iuY2fFg==";
        public
        const string
            GitUrl = "git@github.com:DeCarabas/onceandfuture.git";
        public const string Port = "8080";
        public const string SecretsJson = "secrets.json";
        public const string KeyId = "df3c54d3-a61a-4561-9414-afa9853143d9";
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

    class Revision
    {
        public Revision(string commit, string subject)
        {
            Commit = commit;
            Subject = subject;
        }

        public string Commit { get; }

        public string Subject { get; }
    }

    static class TheConsole
    {
        public static void WriteColor(
            ConsoleColor foreground,
            string message,
            params object[] args)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = foreground;
            try
            {
                Console.Write(message, args);
            }
            finally
            {
                Console.ForegroundColor = oldColor;
            }
        }

        internal static void WriteLineColor(
            ConsoleColor foreground,
            string message,
            params object[] args)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = foreground;
            try
            {
                Console.WriteLine(message, args);
            }
            finally
            {
                Console.ForegroundColor = oldColor;
            }
        }
    }

    class Program
    {
        static readonly RegionEndpoint region = RegionEndpoint.USWest2;
        static readonly AmazonCloudFormationClient cloudFormationClient = new AmazonCloudFormationClient(
                region
            );
        static readonly AmazonAutoScalingClient autoScalingClient = new AmazonAutoScalingClient(
                region
            );
        static readonly AmazonElasticLoadBalancingV2Client elbClient = new AmazonElasticLoadBalancingV2Client(
                region
            );
        static readonly AmazonS3Client s3client = new AmazonS3Client(
                RegionEndpoint.USWest2
            );
        static ProgramOpts Options = new ProgramOpts().AddOption(
                "help",
                "Display this help.",
                o => o.Flag('?')
            ).AddVerb(
                "build",
                "Do a build",
                DoBuild,
                v =>
                    v.AddOption(
                        "commit",
                        "The commit to build.",
                        o => o.AcceptValue()
                    ).AddOption(
                        "force",
                        "Rebuild even if we've already built the specified commit."
                    )
            ).AddVerb(
                "deploy",
                "Deploy to an environment.",
                DoDeploy,
                v =>
                    v.AddOption(
                        "force",
                        "Redeploy even if we've already deployed the latest build."
                    )
            ).AddVerb(
                "encrypt",
                "Encrypt a setting with KMS.",
                DoEncrypt,
                v =>
                    v.AddOption(
                        "value",
                        "The value to encrypt.",
                        o => o.IsRequired()
                    ).AddOption(
                        "name",
                        "The name of the setting to decrypt.",
                        o => o.AcceptValue()
                    ).AddOption(
                        "env",
                        "The environment of the setting to decrypt.",
                        o => o.AcceptValue()
                    )
            ).AddVerb(
                "decrypt",
                "Decrypt a setting with KMS.",
                DoDecrypt,
                v =>
                    v.AddOption(
                        "value",
                        "The value to decrypt.",
                        o => o.AcceptValue()
                    ).AddOption(
                        "name",
                        "The name of the setting to decrypt.",
                        o => o.AcceptValue()
                    ).AddOption(
                        "env",
                        "The environment of the setting to decrypt.",
                        o => o.AcceptValue()
                    )
            );

        static string CreateStack(StackBase stack)
        {
            Console.WriteLine(
                "Creating {0} stack for build {1}",
                stack.StackType,
                stack.Version
            );

            //string template = CreateBuildTemplate(stackName, commit);
            //string outFile = String.Format("debug-{0}-{1}-template.json", stack.StackType, stack.Environment);
            //File.WriteAllText(outFile, template);
            Console.WriteLine("    Creating stack {0}", stack.Name);

            CreateStackResponse response = cloudFormationClient.CreateStackAsync(
                    new CreateStackRequest
                    {
                        OnFailure = OnFailure.DELETE,
                        StackName = stack.Name,
                        Parameters = stack.Parameters,
                        TemplateBody = stack.GetTemplate(),
                        TimeoutInMinutes = 20,
                        Tags = stack.Tags,
                    }
                ).Result;

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

        static void PrintStackEvents(List<StackEvent> events)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            foreach (StackEvent evt in events)
            {
                ConsoleColor statusColor;
                if (
                    evt.ResourceStatus == ResourceStatus.CREATE_FAILED ||
                    evt.ResourceStatus == ResourceStatus.DELETE_FAILED ||
                    evt.ResourceStatus == ResourceStatus.UPDATE_FAILED
                )
                {
                    statusColor = ConsoleColor.Red;
                }
                else if (
                    evt.ResourceStatus == ResourceStatus.CREATE_COMPLETE ||
                    evt.ResourceStatus == ResourceStatus.DELETE_COMPLETE ||
                    evt.ResourceStatus == ResourceStatus.UPDATE_COMPLETE
                )
                {
                    statusColor = ConsoleColor.Green;
                }
                else
                {
                    statusColor = defaultColor;
                }

                Console.ForegroundColor = statusColor;
                Console.WriteLine(
                    "{0,-20} {1,-20} {2,-40} {3} - {4}",
                    evt.Timestamp,
                    evt.ResourceStatus,
                    evt.ResourceType,
                    evt.LogicalResourceId,
                    evt.ResourceStatusReason
                );
            }

            Console.ForegroundColor = defaultColor;
        }

        static bool WaitForStackCreated(string stackId)
        {
            Console.WriteLine("Waiting for doneness of {0}...", stackId);

            string lastId = null;
            bool? success = null;

            do
            {
                SleepWithAnimation(TimeSpan.FromSeconds(5));
                var response = cloudFormationClient.DescribeStackEventsAsync(
                        new DescribeStackEventsRequest { StackName = stackId, }
                    ).Result;
                if (response.StackEvents.Count == 0)
                {
                    continue;
                }

                var newEvents = new List<StackEvent>();
                foreach (StackEvent evt in response.StackEvents)
                {
                    if (evt.EventId == lastId)
                    {
                        break;
                    }

                    newEvents.Add(evt);
                    if (evt.PhysicalResourceId == stackId)
                    {
                        if (
                            evt.ResourceStatus == ResourceStatus.CREATE_COMPLETE
                        )
                        {
                            success = true;
                        }
                        else if (
                            evt.ResourceStatus == ResourceStatus.DELETE_COMPLETE
                        )
                        {
                            success = false;
                        }
                        else if (
                            evt.ResourceStatus == ResourceStatus.CREATE_FAILED
                        )
                        {
                            success = false;
                        }
                    }
                }

                newEvents.Reverse();
                PrintStackEvents(newEvents);
                lastId = response.StackEvents[0].EventId;
            }
            while (success == null);

            Console.WriteLine(
                "Stack creation {0}",
                success.Value ? "SUCCEEDED" : "FAILED"
            );
            return success.Value;
        }

        static bool WaitForStackDeleted(string stackId)
        {
            Console.WriteLine("Waiting for doneness of {0}...", stackId);

            string lastId = null;
            bool? success = null;

            do
            {
                SleepWithAnimation(TimeSpan.FromSeconds(5));
                var response = cloudFormationClient.DescribeStackEventsAsync(
                        new DescribeStackEventsRequest { StackName = stackId, }
                    ).Result;
                if (response.StackEvents.Count == 0)
                {
                    continue;
                }

                if (lastId == null)
                {
                    // Assume the last CREATE_COMPLETE or UPDATE_COMPLETE is the lastId. Fortunately the events
                    // come out newest-to-oldest.
                    foreach (StackEvent evt in response.StackEvents)
                    {
                        if (
                            evt.ResourceStatus == ResourceStatus.CREATE_COMPLETE ||
                            evt.ResourceStatus == ResourceStatus.UPDATE_COMPLETE
                        )
                        {
                            lastId = evt.EventId;
                            break;
                        }
                    }
                }

                var newEvents = new List<StackEvent>();
                foreach (StackEvent evt in response.StackEvents)
                {
                    if (evt.EventId == lastId)
                    {
                        break;
                    }

                    newEvents.Add(evt);
                    if (evt.PhysicalResourceId == stackId)
                    {
                        if (
                            evt.ResourceStatus == ResourceStatus.DELETE_COMPLETE
                        )
                        {
                            success = true;
                        }
                        else if (
                            evt.ResourceStatus == ResourceStatus.DELETE_FAILED
                        )
                        {
                            success = false;
                        }
                    }
                }

                newEvents.Reverse();
                PrintStackEvents(newEvents);
                lastId = response.StackEvents[0].EventId;
            }
            while (success == null);

            Console.WriteLine(
                "Stack deletion {0}",
                success.Value ? "SUCCEEDED" : "FAILED"
            );
            return success.Value;
        }

        static bool WaitForStackHealthy(string stackId)
        {
            Console.WriteLine("Getting scaling group from {0}...", stackId);
            DescribeStackResourceResponse response;
            response =
                cloudFormationClient.DescribeStackResourceAsync(
                    new DescribeStackResourceRequest
                    {
                        StackName = stackId,
                        LogicalResourceId = "AutoScalingGroup",
                    }
                ).Result;
            string autoScalingGroupId = response.StackResourceDetail.PhysicalResourceId;

            Console.WriteLine(
                "Instances from scaling group {0}...",
                autoScalingGroupId
            );
            var response2 = autoScalingClient.DescribeAutoScalingGroupsAsync(
                    new DescribeAutoScalingGroupsRequest
                    {
                        AutoScalingGroupNames = { autoScalingGroupId },
                    }
                ).Result;
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

        static bool CheckInstancesHealthy(
            string targetGroup,
            List<Instance> instances)
        {
            var response3 = elbClient.DescribeTargetHealthAsync(
                    new DescribeTargetHealthRequest
                    {
                        TargetGroupArn = targetGroup,
                    }
                ).Result;

            Dictionary<string, TargetHealthDescription> healths;
            healths =
                response3.TargetHealthDescriptions.ToDictionary(
                    k => k.Target.Id
                );
            foreach (Instance instance in instances)
            {
                if (
                    !healths.TryGetValue(
                        instance.InstanceId,
                        out TargetHealthDescription health
                    )
                )
                {
                    Console.WriteLine(
                        "{0,-20} {0} not in health yet...",
                        DateTime.Now,
                        instance.InstanceId
                    );
                    return false;
                }

                if (health.TargetHealth.State != TargetHealthStateEnum.Healthy)
                {
                    Console.WriteLine(
                        "{0,-20} {1} in state {2} : {3}",
                        DateTime.Now,
                        instance.InstanceId,
                        health.TargetHealth.State,
                        health.TargetHealth.Reason
                    );
                    return false;
                }
            }

            TheConsole.WriteLineColor(
                ConsoleColor.Green,
                "{0,-20} All instances healthy!",
                DateTime.Now
            );
            return true;
        }

        static Stack[] GetStacksByEnvironmentAndType(
            string environment,
            string type) =>
            cloudFormationClient.DescribeStacksAsync().Result.Stacks.Where(
                s =>
                    s.Tags.Any(
                        tag =>
                            tag.Key == "environment" && tag.Value == environment
                    )
            ).Where(
                s =>
                    s.Tags.Any(
                        tag => tag.Key == "stack-type" && tag.Value == type
                    )
            ).ToArray();

        static string[] GetOldReleaseStacks(string environment) =>
            GetStacksByEnvironmentAndType(environment, "release").Select(
                st => st.StackId
            ).ToArray();

        static void DeleteStack(string stackId)
        {
            Console.WriteLine("Marking {0} for deletion...", stackId);
            cloudFormationClient.DeleteStackAsync(
                new DeleteStackRequest { StackName = stackId }
            ).Wait();
            WaitForStackDeleted(stackId);
        }

        static List<Dictionary<string, object>> LoadSecrets(string environment)
        {
            return JsonConvert.DeserializeObject<
                List<Dictionary<string, object>>
            >(File.ReadAllText(Configuration.SecretsJson)).Where(
                d =>
                    (string)(d["env"]) == environment ||
                    (string)(d["env"]) == "all"
            ).Select(
                d =>
                    new Dictionary<string, object>
                    {
                        { "KEY", d["name"] },
                        { "VALUE", d["value"] }
                    }
            ).ToList();
        }

        static string DecryptValue(string value)
        {
            var client = new AmazonKeyManagementServiceClient(
                    Amazon.RegionEndpoint.USWest2
                );
            var response = client.DecryptAsync(
                    new Amazon.KeyManagementService.Model.DecryptRequest
                    {
                        CiphertextBlob = new MemoryStream(
                                Convert.FromBase64String(value)
                            ),
                        EncryptionContext = {
                                { "application", Configuration.Application },
                            },
                    }
                ).Result;

            byte[] bytes = new byte[response.Plaintext.Length];
            response.Plaintext.Read(bytes, 0, bytes.Length);
            return System.Text.Encoding.UTF8.GetString(bytes).Trim();
        }

        static string EncryptValue(string value)
        {
            var client = new AmazonKeyManagementServiceClient(
                    Amazon.RegionEndpoint.USWest2
                );
            var response = client.EncryptAsync(
                    new Amazon.KeyManagementService.Model.EncryptRequest
                    {
                        Plaintext = new MemoryStream(
                                System.Text.Encoding.UTF8.GetBytes(value)
                            ),
                        KeyId = Configuration.KeyId,
                        EncryptionContext = {
                                { "application", Configuration.Application },
                            },
                    }
                ).Result;

            byte[] bytes = new byte[response.CiphertextBlob.Length];
            response.CiphertextBlob.Read(bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        static BuildVersion GetLastVersion()
        {
            List<S3Object> allObjects = new List<S3Object>();
            string nextMarker = null;
            do
            {
                var response = s3client.ListObjectsAsync(
                        new ListObjectsRequest
                        {
                            BucketName = Configuration.BaseBucketName,
                            Marker = nextMarker,
                            Prefix = "artifacts/onceandfuture",
                        }
                    ).Result;
                allObjects.AddRange(response.S3Objects);
                nextMarker = response.IsTruncated ? response.NextMarker : null;
            }
            while (nextMarker != null);

            if (allObjects.Count == 0)
            {
                return null;
            }

            // Compare keys in reverse so largest key is first.
            allObjects.Sort((x, y) => String.Compare(y.Key, x.Key));
            S3Object newest = allObjects[0];

            var imageRegex = new Regex(
                    "artifacts/onceandfuture/([0-9]+)/([0-9]+Z)/([a-f0-9]+).tar.lz4"
                );
            Match match = imageRegex.Match(newest.Key);
            return new BuildVersion
            {
                BuildDate = match.Groups[1].Value,
                BuildTime = match.Groups[2].Value,
                Commit = match.Groups[3].Value,
            };
        }

        static string GetDeployedCommit(string environment)
        {
            var commits = from
                    stack
                    in GetStacksByEnvironmentAndType(environment, "release")
                orderby stack.LastUpdatedTime descending
                from tag in stack.Tags
                where tag.Key == "commit"
                select tag.Value;

            return commits.FirstOrDefault();
        }

        static List<Revision> GetMasterLog()
        {
            // git fetch origin master
            var process = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "log origin/master --format=format:%H|%s --max-count=200",
                        RedirectStandardOutput = true,
                    }
                );

            var revs = new List<Revision>();
            while (true)
            {
                string line = process.StandardOutput.ReadLine();
                if (line == null)
                {
                    break;
                }

                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                string[] parts = line.Split(new char[] { '|' }, 2);
                revs.Add(new Revision(commit: parts[0], subject: parts[1]));
            }

            process.WaitForExit();
            process.Dispose();

            return revs;
        }

        static List<Revision> GetDelta(
            List<Revision> log,
            string start,
            string end)
        {
            List<Revision> delta = null;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Commit == start)
                {
                    delta = new List<Revision>();
                }

                if (delta != null)
                {
                    if (log[i].Commit == end)
                    {
                        break;
                    }

                    delta.Add(log[i]);
                }
            }

            return delta;
        }

        static void PrintChangeLog(List<Revision> log)
        {
            for (int i = 0; i < log.Count; i++)
            {
                TheConsole.WriteColor(
                    ConsoleColor.Green,
                    "    {0}",
                    log[i].Commit.Substring(0, 7)
                );
                Console.WriteLine(" - {0}", log[i].Subject);
            }
        }

        static bool Confirm()
        {
            Console.Write("Ready to proceed? [y/N] ");
            string response = Console.ReadLine();
            return response.StartsWith("y", StringComparison.OrdinalIgnoreCase);
        }

        static int DoBuild(ParsedOpts args)
        {
            DateTime startTime = DateTime.Now;

            List<Revision> log = GetMasterLog();
            string commit = args["commit"].Value ?? log[0].Commit;
            Console.WriteLine(
                "Building and uploading for commit {0}...",
                commit
            );

            BuildVersion lastVersion = GetLastVersion();
            List<Revision> changes = GetDelta(log, commit, lastVersion.Commit);
            if (changes == null)
            {
                Console.WriteLine(
                    "Cannot find commit {0} in the log; aborting!",
                    commit
                );
                return 2;
            }

            if (changes.Count > 0)
            {
                Console.WriteLine("Commits since the last build:");
                PrintChangeLog(changes);
            }
            else if (!args["force"].Flag)
            {
                Console.WriteLine(
                    "Latest build is for this commit, nothing to do."
                );
                return 0;
            }

            if (!Confirm())
            {
                Console.WriteLine("Aborting.");
                return 3;
            }

            DateTime now = DateTime.UtcNow;
            string buildDate = String.Format(
                    "{0:D4}{1:D2}{2:D2}",
                    now.Year,
                    now.Month,
                    now.Day
                );
            string buildTime = String.Format(
                    "{0:D2}{1:D2}{2:D2}Z",
                    now.Hour,
                    now.Minute,
                    now.Second
                );
            var version = new BuildVersion
                {
                    BuildDate = buildDate,
                    BuildTime = buildTime,
                    Commit = commit
                };

            Console.WriteLine("Building {0}", version);
            string stackId = CreateStack(new BuildStack(version));
            bool succeeded = WaitForStackCreated(stackId);
            DeleteStack(stackId);
            Console.WriteLine("BUILD {0}", succeeded ? "SUCCESS" : "FAILED");
            return succeeded ? 0 : 1;
        }

        static int DoDeploy(ParsedOpts args)
        {
            const string environment = "qa";

            DateTime startTime = DateTime.Now;
            BuildVersion version = GetLastVersion();
            string deployedCommit = GetDeployedCommit(environment);

            if (deployedCommit != version.Commit)
            {
                List<Revision> log = GetMasterLog();
                List<Revision> changes = GetDelta(
                        log,
                        log[0].Commit,
                        deployedCommit
                    );
                if (changes != null && changes.Count > 0)
                {
                    Console.WriteLine("Commits to be deployed:");
                    PrintChangeLog(changes);
                }
            }
            else if (!args["force"].Flag)
            {
                Console.WriteLine(
                    "Latest release is for this build, nothing to do."
                );
                return 0;
            }

            if (!Confirm())
            {
                Console.WriteLine("Aborting.");
                return 3;
            }

            Console.WriteLine(
                "Deploying {0} to {1} @ {2}",
                version,
                environment,
                startTime
            );
            string[] oldStackIds = GetOldReleaseStacks(environment);
            if (oldStackIds.Length > 0)
            {
                Console.WriteLine("Existing stacks are:");
                for (int i = 0; i < oldStackIds.Length; i++)
                {
                    Console.WriteLine("    {0}", oldStackIds[i]);
                }
            }

            string stackId = CreateStack(new ReleaseStack(version, environment));
            bool succeeded = WaitForStackCreated(stackId) &&
                WaitForStackHealthy(stackId);
            if (succeeded)
            {
                Console.WriteLine("CREATE SUCCESS");
                Console.WriteLine("Cleaning up old stacks...");
                for (int i = 0; i < oldStackIds.Length; i++)
                {
                    DeleteStack(oldStackIds[i]);
                }

                Console.WriteLine("DEPLOY SUCCESS");
            }
            else
            {
                Console.WriteLine("FAILED -- Cleaning Up");
                DeleteStack(stackId);
            }

            return succeeded ? 0 : 1;
        }

        static int DoEncrypt(ParsedOpts args)
        {
            string value = EncryptValue(args["value"].Value);

            string name = args["name"].Value;
            string env = args["env"].Value;
            if (!String.IsNullOrEmpty(name) || !String.IsNullOrEmpty(env))
            {
                if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(env))
                {
                    Console.Error.WriteLine(
                        "Must specify both of 'name' and 'env'."
                    );
                    return -1;
                }

                var secretList = JsonConvert.DeserializeObject<JArray>(
                        File.ReadAllText(Configuration.SecretsJson)
                    );

                bool found = false;
                foreach (JObject secret in secretList)
                {
                    string secretEnvironment = secret.Value<string>("env");
                    string secretName = secret.Value<string>("name");
                    if (secretEnvironment == env && secretName == name)
                    {
                        secret["value"] = JValue.CreateString(value);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    secretList.Add(
                        new JObject(
                            new JProperty("name", name),
                            new JProperty("env", env),
                            new JProperty("ord", secretList.Count),
                            new JProperty("value", value)
                        )
                    );
                }

                File.WriteAllText(
                    Configuration.SecretsJson,
                    JsonConvert.SerializeObject(secretList, Formatting.Indented)
                );
            }
            else
            {
                Console.WriteLine("{0}", value);
            }

            return 0;
        }

        static int DoDecrypt(ParsedOpts args)
        {
            string value = args["value"].Value;
            if (String.IsNullOrEmpty(value))
            {
                string name = args["name"].Value;
                string env = args["env"].Value;

                if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(env))
                {
                    Console.Error.WriteLine(
                        "Must specify either 'value' or 'name' and 'env'."
                    );
                    return -1;
                }

                var secretList = JsonConvert.DeserializeObject<JArray>(
                        File.ReadAllText(Configuration.SecretsJson)
                    );
                foreach (JObject secret in secretList)
                {
                    string secretEnvironment = secret.Value<string>("env");
                    string secretName = secret.Value<string>("name");
                    if (secretEnvironment == env && secretName == name)
                    {
                        value = secret.Value<string>("value");
                        break;
                    }
                }

                if (String.IsNullOrEmpty(value))
                {
                    Console.Error.WriteLine(
                        "Setting '{0}' in environment '{1}' not found.",
                        name,
                        env
                    );
                    return -2;
                }
            }

            Console.WriteLine("{0}", DecryptValue(value));
            return 0;
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

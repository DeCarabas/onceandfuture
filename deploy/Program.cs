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
using onceandfuture;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace deploy
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

    class BuildTag
    {
        public string BuildDate;
        public string BuildTime;
        public string Commit;

        public override string ToString()
        {
            return String.Format("{0}/{1}/{2}", BuildDate, BuildTime, Commit);
        }
    }

    class Program
    {
        static AmazonCloudFormationClient cloudFormationClient
            = new AmazonCloudFormationClient(RegionEndpoint.USWest2);
        static AmazonAutoScalingClient autoScalingClient
            = new AmazonAutoScalingClient(RegionEndpoint.USWest2);
        static AmazonElasticLoadBalancingV2Client elbClient
            = new AmazonElasticLoadBalancingV2Client(RegionEndpoint.USWest2);

        static ProgramOpts Options = new ProgramOpts()
            .AddOption("help", "Display this help.", o => o.Flag('?'))
            .AddVerb("build", "Do a build", DoBuild, v => v
                .AddOption("commit", "The commit to build.", o => o.AcceptValue())
                .AddOption("force", "Rebuild even if we've already built the specified commit.")
            )
            .AddVerb("deploy", "Deploy to an environment.", DoDeploy, v => v
            )
            ;

        static string GetReleaseId(string commit) => commit.Substring(0, 7);

        static object CreateBuildStartupScript(string stackName, BuildTag build)
        {
            Template startupTemplate = new Template(File.ReadAllText("builder.sh"));

            string script = startupTemplate.Format(new Dictionary<string, object>
            {
                { "STACKNAME", stackName },
                { "APP", Configuration.Application },
                { "SHA", build.Commit },
                { "S3_BUCKET", Configuration.BaseBucketName },
                { "BUILD_DATE", build.BuildDate },
                { "BUILD_TIME", build.BuildTime },
                { "GIT_URL", Configuration.GitUrl },
                { "GIT_KEY", Configuration.GithubKey },
            });
            script = script.Replace("\r\n", "\n");

            using (var scriptOut = new MemoryStream())
            {
                using (var gzs = new GZipStream(scriptOut, CompressionLevel.Optimal, leaveOpen: true))
                {
                    byte[] scriptBytes = Encoding.UTF8.GetBytes(script);
                    gzs.Write(scriptBytes, 0, scriptBytes.Length);
                    gzs.Flush();
                }

                byte[] bytes = new byte[scriptOut.Length];
                scriptOut.Position = 0;
                scriptOut.Read(bytes, 0, bytes.Length);

                // File.WriteAllBytes("build.sh.gz", bytes);
                return Convert.ToBase64String(bytes);
            }
        }

        static string CreateBuildTemplate(string stackName, BuildTag build)
        {
            var template = new
            {
                AWSTemplateFormatVersion = "2010-09-09",
                Description = "A Dotyliner build stack. Do not manually delete.",
                Resources = new
                {
                    BuilderInstance = new
                    {
                        Type = "AWS::EC2::Instance",
                        Properties = new
                        {
                            UserData = CreateBuildStartupScript(stackName, build),
                            Tags = new[] { new { Key = "Name", Value = new { Ref = "AWS::StackName" } } },
                            InstanceInitiatedShutdownBehavior = "terminate",
                            ImageId = "ami-8ca83fec",
                            BlockDeviceMappings = new[]
                            {
                                new
                                {
                                    DeviceName = "/dev/xvda",
                                    Ebs = new
                                    {
                                        DeleteOnTermination = true,
                                        SnapshotId ="snap-066b5016ee2261563",
                                        VolumeSize = 8,
                                        VolumeType = "gp2"
                                    },
                                }
                            },
                            IamInstanceProfile = "qa-onceandfuture-BuilderIAMInstanceProfile-1CY8JHUXVJRAX", // TODO PROD
                            InstanceType = "t2.medium",
                            NetworkInterfaces = new[]
                            {
                                new
                                {
                                    SubnetId = "subnet-951b6fcd",
                                    DeviceIndex = 0,
                                    GroupSet = new[] { "sg-488bd131" }, // TODO PROD
                                    DeleteOnTermination = true,
                                    AssociatePublicIpAddress = true,
                                },
                            },
                        },
                        CreationPolicy = new { ResourceSignal = new { Count = 1, Timeout = "PT30M" } },
                    },
                },
            };

            using (var w = new StringWriter())
            {
                JsonSerializer.Create().Serialize(w, template);
                return w.ToString();
            }
        }

        static string CreateBuildStack(DateTime startTime, BuildTag build)
        {
            Console.WriteLine("Creating builder stack for commit {0}", build.Commit);
            string stackName = String.Join("-", new string[] {
                "builder",
                Configuration.Application,
                "doty",
                GetReleaseId(build.Commit),
                startTime.Year.ToString(),
                startTime.Month.ToString(),
                startTime.Day.ToString(),
                startTime.Hour.ToString(),
                startTime.Minute.ToString(),
                startTime.Second.ToString(),
            });

            //string template = CreateBuildTemplate(stackName, commit);
            //File.WriteAllText("debug-build-template.json", template);

            Console.WriteLine("Creating stack {0}", stackName);

            CreateStackResponse response = cloudFormationClient.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                StackName = stackName,
                TemplateBody = CreateBuildTemplate(stackName, build),
                TimeoutInMinutes = 20,
                Tags =
                {
                    new Amazon.CloudFormation.Model.Tag { Key = "application", Value = Configuration.Application },
                    new Amazon.CloudFormation.Model.Tag { Key = "commit", Value = build.Commit },
                    new Amazon.CloudFormation.Model.Tag { Key = "stack-type", Value = "builder" },
                },
            }).Result;

            string stackId = response.StackId;
            Console.WriteLine("Created {0}", stackId);
            return stackId;
        }

        static string CreateReleaseStartupScript(string stackName, string environment, BuildTag tag)
        {
            List<Dictionary<string, object>> secrets = LoadSecrets(environment);
            Template startupTemplate = new Template(File.ReadAllText("startup.sh"));

            string script = startupTemplate.Format(new Dictionary<string, object>
            {
                { "STACKNAME", stackName },
                { "APP", Configuration.Application },
                { "ENV", environment },
                { "RELEASE", GetReleaseId(tag.Commit) },
                { "SHA", tag.Commit },
                { "PORT", Configuration.Port },
                { "S3_BUCKET", Configuration.BaseBucketName },
                { "SECRETS", secrets },
                { "BUILD_DATE", tag.BuildDate },
                { "BUILD_TIME", tag.BuildTime },
            });
            script = script.Replace("\r\n", "\n");

            using (var scriptOut = new MemoryStream())
            {
                using (var gzs = new GZipStream(scriptOut, CompressionLevel.Optimal, leaveOpen: true))
                {
                    byte[] scriptBytes = Encoding.UTF8.GetBytes(script);
                    gzs.Write(scriptBytes, 0, scriptBytes.Length);
                    gzs.Flush();
                }

                byte[] bytes = new byte[scriptOut.Length];
                scriptOut.Position = 0;
                scriptOut.Read(bytes, 0, bytes.Length);

                //File.WriteAllBytes("blah.sh.gz", bytes);
                return Convert.ToBase64String(bytes);
            }
        }

        static string CreateReleaseTemplate(string stackName, string environment, BuildTag build)
        {
            var template = new
            {
                AWSTemplateFormatVersion = "2010-09-09",
                Description = "A Dotyliner release stack. Do not manually delete.",
                Parameters = new
                {
                    Size = new { Description = "The number of instances to run.", Type = "Number" },
                },
                Resources = new
                {
                    LaunchConfiguration = new
                    {
                        Type = "AWS::AutoScaling::LaunchConfiguration",
                        Properties = new
                        {
                            AssociatePublicIpAddress = true,
                            BlockDeviceMappings = new[]
                            {
                                new
                                {
                                    DeviceName = "/dev/xvda",
                                    Ebs = new
                                    {
                                        DeleteOnTermination = true,
                                        SnapshotId ="snap-066b5016ee2261563",
                                        VolumeSize = 8,
                                        VolumeType = "gp2"
                                    },
                                }
                            },
                            IamInstanceProfile = "qa-onceandfuture-IAMInstanceProfile-16NZXY5JUOMO2", // TODO PROD
                            ImageId = "ami-8ca83fec",
                            InstanceType = "t2.micro",
                            SecurityGroups = new[] { "sg-2b8bd152" }, // TODO PROD
                            UserData = CreateReleaseStartupScript(stackName, environment, build),
                            KeyName = "standard key what",
                        }
                    },
                    AutoScalingGroup = new
                    {
                        Type = "AWS::AutoScaling::AutoScalingGroup",
                        Properties = new
                        {
                            MinSize = new { Ref = "Size" },
                            TargetGroupARNs = new[]
                            {
                                "arn:aws:elasticloadbalancing:us-west-2:964037288281:targetgroup/qa-onceandfuture/b4affac402d6d901",
                            },
                            MetricsCollection = new[] { new { Granularity = "1Minute" } },
                            DesiredCapacity = new { Ref = "Size" },
                            Tags = new[] { new { Key = "Name", Value = new { Ref = "AWS::StackName" }, PropagateAtLaunch = true } },
                            VPCZoneIdentifier = new[] { "subnet-d79b7ab0", "subnet-76546f00", "subnet-951b6fcd" },
                            HealthCheckType = "EC2",
                            MaxSize = new { Ref = "Size" },
                            LaunchConfigurationName = new { Ref = "LaunchConfiguration" }
                        },
                        CreationPolicy = new
                        {
                            ResourceSignal = new
                            {
                                Count = new { Ref = "Size" },
                                Timeout = "PT15M",
                            },
                        },
                    },
                },
            };

            using (var w = new StringWriter())
            {
                JsonSerializer.Create().Serialize(w, template);
                return w.ToString();
            }
        }

        static string CreateReleaseStack(DateTime startTime, string environment, BuildTag build)
        {
            string stackName = String.Join("-", new string[] {
                environment,
                "onceandfuture",
                "doty",
                GetReleaseId(build.Commit),
                startTime.Year.ToString(),
                startTime.Month.ToString(),
                startTime.Day.ToString(),
                startTime.Hour.ToString(),
                startTime.Minute.ToString(),
                startTime.Second.ToString(),
            });

            //string template = CreateTemplate(stackName, environment, commit);
            //File.WriteAllText("debug-template.json", template);

            Console.WriteLine("Creating stack {0}", stackName);

            CreateStackResponse response = cloudFormationClient.CreateStackAsync(new CreateStackRequest
            {
                Parameters = {
                    new Parameter { ParameterKey = "Size", ParameterValue = "1" },
                },
                OnFailure = OnFailure.ROLLBACK,
                StackName = stackName,
                TemplateBody = CreateReleaseTemplate(stackName, environment, build),
                TimeoutInMinutes = 20,
                Tags =
                {
                    new Amazon.CloudFormation.Model.Tag { Key = "environment", Value = environment },
                    new Amazon.CloudFormation.Model.Tag { Key = "application", Value = Configuration.Application },
                    new Amazon.CloudFormation.Model.Tag { Key = "release", Value = GetReleaseId(build.Commit) },
                    new Amazon.CloudFormation.Model.Tag { Key = "stack-type", Value = "release" },
                    new Amazon.CloudFormation.Model.Tag { Key = "deploy", Value = "49b" },
                },
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

            Dictionary<string, TargetHealthDescription> healths = response3.TargetHealthDescriptions.ToDictionary(k => k.Target.Id);
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

        static BuildTag GetLastBuild()
        {
            var s3client = new AmazonS3Client(RegionEndpoint.USWest2);
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
            return new BuildTag
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

            BuildTag build = GetLastBuild();
            if (build.Commit == commit)
            {
                Console.WriteLine("Latest build is for this commit, nothing to do.");
                return 0;
            }

            DateTime now = DateTime.UtcNow;
            string buildDate = String.Format("{0:D4}{1:D2}{2:D2}", now.Year, now.Month, now.Day);
            string buildTime = String.Format("{0:D2}{1:D2}{2:D2}Z", now.Hour, now.Minute, now.Second);
            build = new BuildTag { BuildDate = buildDate, BuildTime = buildTime, Commit = commit };

            Console.WriteLine("Building {0}", build);
            string stackId = CreateBuildStack(startTime, build);
            bool succeeded = WaitForStackCreated(stackId);
            DeleteStack(stackId);
            Console.WriteLine("BUILD {0}", succeeded ? "SUCCESS" : "FAILED");
            return succeeded ? 0 : 1;
        }

        static int DoDeploy(ParsedOpts args)
        {
            const string environment = "qa";

            DateTime startTime = DateTime.Now;
            BuildTag build = GetLastBuild();

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

            string stackId = CreateReleaseStack(startTime, environment, build);
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
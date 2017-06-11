using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace deploy
{
    class Program
    {
        static AmazonCloudFormationClient cloudFormationClient
            = new AmazonCloudFormationClient(RegionEndpoint.USWest2);
        static AmazonAutoScalingClient autoScalingClient
            = new AmazonAutoScalingClient(RegionEndpoint.USWest2);
        static AmazonElasticLoadBalancingV2Client elbClient
            = new AmazonElasticLoadBalancingV2Client(RegionEndpoint.USWest2);

        static string GetReleaseId(string commit) => commit.Substring(0, 7);

        static string CreateStartupScript(string stackName, string environment, string commit)
        {
            List<Dictionary<string, object>> secrets = LoadSecrets(environment);
            Template startupTemplate = new Template(File.ReadAllText("startup.sh"));

            string script = startupTemplate.Format(new Dictionary<string, object>
            {
                { "STACKNAME", stackName },
                { "APP", "onceandfuture" },
                { "ENV", environment },
                { "RELEASE", GetReleaseId(commit) },
                { "SHA", commit },
                { "PORT", "8080" },
                { "S3_BUCKET", "base-storagebucket-1p0p3r2s2844b" },
                { "SECRETS", secrets },
                { "BUILD_DATE", "20170513" },
                { "BUILD_TIME", "184921Z" },
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

        static string CreateTemplate(string stackName, string environment, string commit)
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
                            IamInstanceProfile = "qa-onceandfuture-IAMInstanceProfile-16NZXY5JUOMO2",
                            ImageId = "ami-8ca83fec",
                            InstanceType = "t2.micro",
                            SecurityGroups = new[] { "sg-2b8bd152" },
                            UserData = CreateStartupScript(stackName, environment, commit),
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

        static string CreateStack(DateTime startTime, string environment, string commit)
        {
            string stackName = String.Join("-", new string[] {
                environment,
                "onceandfuture",
                "doty",
                GetReleaseId(commit),
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
                TemplateBody = CreateTemplate(stackName, environment, commit),
                TimeoutInMinutes = 20,
                Tags =
                {
                    new Amazon.CloudFormation.Model.Tag { Key = "environment", Value = environment },
                    new Amazon.CloudFormation.Model.Tag { Key = "application", Value = "onceandfuture" },
                    new Amazon.CloudFormation.Model.Tag { Key = "release", Value = GetReleaseId(commit) },
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

        static void BuildAndUpload(string commit)
        {
            // TODO: Sync and build the given SHA, upload to S3.
        }

        static string[] GetOldStackIds(string environment)
        {
            var response = cloudFormationClient.DescribeStacksAsync().Result;
            return response.Stacks.Where(
                s => s.Tags.Any(tag => tag.Key == "environment" && tag.Value == environment)
            ).Where(
                s => s.Tags.Any(tag => tag.Key == "stack-type" && tag.Value == "release")
            ).Select(
                s => s.StackId
            ).ToArray();
        }

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

        static void Main(string[] args)
        {
            try
            {
                const string environment = "qa";
                const string commit = "0f4949b7efe939ae14ed6b1a0953dcf3649ae686";

                DateTime startTime = DateTime.Now;

                Console.WriteLine("Building and uploading for commit {0}...", commit);
                BuildAndUpload(commit);

                Console.WriteLine("Deploying commit {0} to {1} @ {2}", commit, environment, startTime);
                string[] oldStackIds = GetOldStackIds(environment);
                if (oldStackIds.Length > 0)
                {
                    Console.WriteLine("Existing stacks are:");
                    for (int i = 0; i < oldStackIds.Length; i++)
                    {
                        Console.WriteLine("    {0}", oldStackIds[i]);
                    }
                }

                string stackId = CreateStack(startTime, environment, commit);
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
            }
            catch (Exception e)
            {
                Console.WriteLine("SAD: {0}", e);
            }
        }
    }
}
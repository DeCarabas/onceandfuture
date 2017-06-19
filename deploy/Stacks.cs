using Amazon.CloudFormation.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OnceAndFuture.Deployment
{
    abstract class StackBase
    {
        public abstract string Environment { get; }

        public abstract string StackType { get; }

        public virtual List<Parameter> Parameters => new List<Parameter>();

        public virtual List<Tag> GetTags(BuildVersion build)
        {
            return new List<Tag> {
                new Amazon.CloudFormation.Model.Tag { Key = "application", Value = Configuration.Application },
                new Amazon.CloudFormation.Model.Tag { Key = "commit", Value = build.Commit },
                new Amazon.CloudFormation.Model.Tag { Key = "environment", Value = Environment },
                new Amazon.CloudFormation.Model.Tag { Key = "stack-type", Value = "builder" },
            };
        }

        public abstract string GetTemplate(string stackName, BuildVersion build);
    }

    class BuildStack : StackBase
    {
        public override string Environment => "build";

        public override string StackType => "builder";

        public override string GetTemplate(string stackName, BuildVersion build)
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
                            UserData = this.CreateStartupScript(stackName, build),
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

        string CreateStartupScript(string stackName, BuildVersion build)
        {
            TextTemplate startupTemplate = new TextTemplate(File.ReadAllText("builder.sh"));

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
    }

    class ReleaseStack : StackBase
    {
        readonly string environment;

        public ReleaseStack(string environment)
        {
            this.environment = environment;
        }

        public override string Environment => this.environment;

        public override List<Parameter> Parameters => new List<Parameter>
        {
            new Parameter { ParameterKey="Size", ParameterValue="1" },
        };

        public override string StackType => "release";

        public override string GetTemplate(string stackName, BuildVersion build)
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
                            UserData = CreateReleaseStartupScript(stackName, build),
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
                            Tags = new[] {
                                new { Key = "Name", Value = new { Ref = "AWS::StackName" }, PropagateAtLaunch = true }
                            },
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

        string CreateReleaseStartupScript(string stackName, BuildVersion tag)
        {
            List<Dictionary<string, object>> secrets = LoadSecrets();
            TextTemplate startupTemplate = new TextTemplate(File.ReadAllText("startup.sh"));

            string script = startupTemplate.Format(new Dictionary<string, object>
            {
                { "STACKNAME", stackName },
                { "APP", Configuration.Application },
                { "ENV", this.environment },
                { "RELEASE", tag.Release },
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

        List<Dictionary<string, object>> LoadSecrets()
        {
            return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
                File.ReadAllText("secrets.json")
            )
            .Where(
                d => (string)(d["env"]) == this.environment || (string)(d["env"]) == "all"
            )
            .Select(
                d => new Dictionary<string, object> { { "KEY", d["name"] }, { "VALUE", d["value"] } }
            ).ToList();
        }
    }
}

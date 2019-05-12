#!/bin/bash
set -euo pipefail
IFS=$'\n\t'
function signal_done {
    /opt/aws/bin/cfn-signal -e $? --stack {{STACKNAME}} --resource AutoScalingGroup --region us-west-2
}
trap signal_done EXIT
mkfifo /tmp/fifo.setup
( logger -s -t setup < /tmp/fifo.setup & )
exec > /tmp/fifo.setup
exec 2>&1
set -v

echo "assumeyes=1" >> /etc/yum.conf
yum install rsyslog
yum update --security

echo '$SystemLogRateLimitInterval 0' >> /etc/rsyslog.conf
echo '$SystemLogRateLimitBurst 0' >> /etc/rsyslog.conf
systemctl restart rsyslog

cat << "EOF" > /etc/logrotate.conf
daily
rotate 1
dateext
create
include /etc/logrotate.d
/var/log/wtmp {
monthly
create 0664 root utmp
minsize 1M
rotate 1
}

/var/log/btmp {
missingok
monthly
create 0600 root utmp
rotate 1
}
EOF
yum install awslogs docker lz4 amazon-ssm-agent

curl --silent https://s3.dualstack.us-west-2.amazonaws.com/aws-xray-assets.us-west-2/xray-daemon/aws-xray-daemon-1.x.rpm -o /tmp/xray.rpm
yum install /tmp/xray.rpm

cat << "EOF" > /etc/awslogs/awscli.conf
[plugins]
cwlogs = cwlogs
[default]
region = us-west-2
EOF

cat << "EOF" > /etc/awslogs/awslogs.conf
[general]
state_file = /var/lib/awslogs/agent-state
[syslog]
datetime_format = %b %d %H:%M:%S
file = /var/log/messages
buffer_duration = 5000
log_stream_name = {instance_id}
initial_position = start_of_file
log_group_name = /{{ENV}}/{{APP}}
EOF
systemctl start awslogsd
systemctl enable awslogsd.service

cat << "EOF" > /etc/sysconfig/docker
DAEMON_MAXFILES=1048576
OPTIONS="--default-ulimit nofile=1024000:1024000"
EOF

cat << "EOF" > /etc/sysconfig/docker-storage
DOCKER_STORAGE_OPTIONS="-s overlay"
EOF

usermod -a -G docker ec2-user
systemctl enable docker
systemctl start docker
systemctl enable amazon-ssm-agent
systemctl start amazon-ssm-agent

echo 'RELEASE={{RELEASE}}' >> /etc/{{APP}}.env
echo 'SHA={{SHA}}' >> /etc/{{APP}}.env
echo 'PORT={{PORT}}' >> /etc/{{APP}}.env
echo 'APP={{APP}}' >> /etc/{{APP}}.env
echo 'ENV={{ENV}}' >> /etc/{{APP}}.env
echo 'SKYLINER_S3_URL=s3://{{S3_BUCKET}}/{{ENV}}/{{APP}}/' >> /etc/{{APP}}.env

{{#SECRETS}}
cat << "EOF" | base64 --decode > /tmp/{{APP}}-{{KEY}}.enc
{{VALUE}}
EOF
cat << EOF >> /etc/{{APP}}.env
{{KEY}}=$(aws kms decrypt --ciphertext-blob fileb:///tmp/{{APP}}-{{KEY}}.enc --query Plaintext --output text --region us-west-2 --encryption-context application={{APP}} | base64 -d)
EOF
rm /tmp/{{APP}}-{{KEY}}.enc
{{/SECRETS}}

aws configure set s3.signature_version s3v4
aws s3 cp --region us-west-2 s3://{{S3_BUCKET}}/artifacts/{{APP}}/{{BUILD_DATE}}/{{BUILD_TIME}}/{{SHA}}.tar.lz4 - | lz4 -d | docker load
docker images -q | xargs -I % docker tag % {{APP}}
iptables -A INPUT -p tcp -m tcp --dport {{PORT}} -j ACCEPT
iptables -t nat -A PREROUTING -p tcp --dport {{PORT}} -j REDIRECT --to-port {{PORT}}
docker run --detach --net host --log-driver syslog --log-opt tag="{{APP}}" --restart always --env-file /etc/{{APP}}.env {{APP}}

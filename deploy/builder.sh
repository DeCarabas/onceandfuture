#!/bin/bash
set -euo pipefail
IFS=$'\n\t'
function signal_done {
	local exit_code=$?
	sleep 30
	/opt/aws/bin/cfn-signal -e $exit_code --stack {{STACKNAME}} --resource BuilderInstance --region us-west-2
	/sbin/shutdown -h now
}
trap signal_done EXIT

mkfifo /tmp/fifo.build
( logger -s -t build < /tmp/fifo.build & )
exec > /tmp/fifo.build
exec 2>&1
set -v

echo "assumeyes=1" >> /etc/yum.conf
yum update --security

echo '$SystemLogRateLimitInterval 0' >> /etc/rsyslog.conf
echo '$SystemLogRateLimitBurst 0' >> /etc/rsyslog.conf
/etc/init.d/rsyslog restart

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

yum install awslogs docker lz4 amazon-ssm-agent git
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
log_group_name = /builder/{{APP}}
EOF
service awslogs start
chkconfig awslogs on

cat << "EOF" > /etc/sysconfig/docker
DAEMON_MAXFILES=1048576
OPTIONS="--default-ulimit nofile=1024000:1024000"
EOF

cat << "EOF" > /etc/sysconfig/docker-storage
DOCKER_STORAGE_OPTIONS="-s overlay"
EOF

usermod -a -G docker ec2-user
service docker start
chkconfig docker on

/sbin/start amazon-ssm-agent

cat << "EOF" | base64 --decode > /tmp/deploy-key.enc
{{GIT_KEY}}
EOF
cat << EOF >> /root/.ssh/id_ecdsa
$(aws kms decrypt --ciphertext-blob fileb:///tmp/deploy-key.enc --query Plaintext --output text --region us-west-2 --encryption-context application={{APP}} | base64 -d)

EOF
chmod 0400 /root/.ssh/id_ecdsa
ssh-keyscan github.com > /root/.ssh/known_hosts
git clone --depth=50 {{GIT_URL}} /tmp/build
cd /tmp/build
git fetch origin {{SHA}}
git checkout -qf FETCH_HEAD
docker build -t {{APP}} .
docker save {{APP}}| lz4 | aws s3 cp --region us-west-2 --storage-class STANDARD_IA --sse aws:kms --sse-kms-key-id df3c54d3-a61a-4561-9414-afa9853143d9 - s3://{{S3_BUCKET}}/artifacts/{{APP}}/{{BUILD_DATE}}/{{BUILD_TIME}}/{{SHA}}.tar.lz4

#!/usr/bin/env bash

########################################################################################################################
# BASH commands to run in terminal one at a time interactively to ensure things go as expected
########################################################################################################################
cd ~/Downloads

# Install and configure the AWS CLI (https://docs.aws.amazon.com/cli/latest/userguide/cli-chap-install.html)
curl "https://s3.amazonaws.com/aws-cli/awscli-bundle.zip" -o "awscli-bundle.zip"
unzip awscli-bundle.zip
sudo ./awscli-bundle/install -i /usr/local/aws -b /usr/local/bin/aws

# Configure the AWS CLI (region should be us-west-2, use your own personal IAM/AWS login key/secret credentials)
aws configure

# Install ecs cli (https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ECS_CLI_installation.html)

sudo curl -o /usr/local/bin/ecs-cli https://s3.amazonaws.com/amazon-ecs-cli/ecs-cli-darwin-amd64-latest
sudo chmod +x /usr/local/bin/ecs-cli

# Configure ecs cli (https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ECS_CLI_Configuration.html)
# Use your own personal IAM/AWS login key/secret credentials)
ecs-cli configure profile --profile-name rydr --access-key $RYDR_AWS_ACCESS_KEY --secret-key $RYDR_AWS_SECRET_KEY
ecs-cli configure profile default --profile-name rydr

ecs-cli configure --cluster Dev-RydrApi --default-launch-type FARGATE --region us-west-2 --cfn-stack-name ECS-Dev-RydrApi --config-name rydr-dev
ecs-cli configure --cluster Prod-RydrApi --default-launch-type FARGATE --region us-west-2 --cfn-stack-name ECS-Prod-RydrApi --config-name rydr-prod
ecs-cli configure default --config-name rydr-dev

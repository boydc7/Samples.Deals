#!/usr/bin/env bash

# NOTE: Should have configured AWS/ECS CLIs using the ../aws-cli-config.txt file commands first...
# NOTE: Should run the below commands from a terminal in the directory this file is contained in....

# cd ~/src/rydr/devops/ecs/prod

#####################################################################################################################################################################
# Build docker images and push to ECR
#####################################################################################################################################################################

# Build the docker images
docker build -f ../../../api/lib/dockerfiles/rydrapi.prod.Dockerfile -t prod/rydr-api -t 933347060724.dkr.ecr.us-west-2.amazonaws.com/prod/rydr-api ../../../api

# View the images
docker images --filter reference=prod/rydr-*

# Login to ECR
$(aws ecr get-login --no-include-email --region us-west-2)

# Push api to ECR
docker push 933347060724.dkr.ecr.us-west-2.amazonaws.com/prod/rydr-api:latest

# Cleanup untagged ECR images
aws ecr batch-delete-image --repository-name prod/rydr-api --image-ids $(aws ecr list-images --repository-name prod/rydr-api --filter tagStatus=UNTAGGED --query 'imageIds[*]'| tr -d " \t\n\r")


#####################################################################################################################################################################
# Build/start the cluster and task definitions services
#####################################################################################################################################################################

# To create/update the prod cluster (no additional vpc/subnet/etc. resources are created here, we assume they already exist)

# ec2 version
# ecs-cli up --security-group sg-077b771e7a7e79802,sg-0b9ad7c3fe8115651 --subnets subnet-02992fa48811f3d02,subnet-02e3ec31bef58ddb3 --capability-iam --keypair RydrApi --size 1 --instance-type t2.micro --no-associate-public-ip-address --vpc vpc-0384073a69514484f --tags Environment=production,Project=api --cluster Prod-RydrApi --cluster-config rydr-prod --ecs-profile rydr

# Fargate version
 ecs-cli up --security-group sg-077b771e7a7e79802,sg-0b9ad7c3fe8115651 --subnets subnet-02992fa48811f3d02,subnet-02e3ec31bef58ddb3 --vpc vpc-0384073a69514484f --tags Environment=production,Project=api --cluster Prod-RydrApi --cluster-config rydr-prod --ecs-profile rydr


#########################################################
# START the docker services in the cluster
#########################################################

# Create task definitions only (useful with FARGATE and CodeDeploy)
# ecs-cli compose --project-name Prod-RydrApi --file docker-compose.yml --ecs-params ecs-params.yml --cluster-config rydr-prod --ecs-profile rydr create --create-log-groups --cluster-config rydr-prod --ecs-profile rydr --tags Environment=production,Project=api
# NOTE: MUST replace the dynamic api name in the taskdef with "rydrapi" temporarily for this to work...
aws ecs register-task-definition --cli-input-json file://../../taskdef-prod.json

# NOTE: The above only creates the task-definition, then currently have to manually create a service in the cluster, since it uses a 
# blue/green deployment, which basically isn't supported on the cli yet...


# Create task definitions and services and start (ec2 or fargate non-green/blue)
# ecs-cli compose --project-name Prod-RydrApi --file docker-compose.yml --ecs-params ecs-params.yml --cluster-config rydr-prod --ecs-profile rydr service up --create-log-groups --cluster-config rydr-prod --ecs-profile rydr --deployment-min-healthy-percent 50 --deployment-max-percent 200 --tags Environment=production,Project=api

# --target-group-arn arn:aws:elasticloadbalancing:us-west-2:933347060724:targetgroup/external-prod-rydrapi/f3e96a659f2ed3af --health-check-grace-period 45

# --role arn:aws:iam::933347060724:role/aws-service-role/ecs.amazonaws.com/AWSServiceRoleForECS 

#########################################################


# VIEW RUNNING CONTAINERS in cluster
ecs-cli compose --project-name Prod-RydrApi service ps --cluster-config rydr-prod

# VIEW CONTAINER logs (taskId comes from ps command above)
ecs-cli logs --task-id _PUT_TASK_ID_HERE_ --follow --cluster-config rydr-prod

# SCALE TASKS up or down (2 here should result in 2 running containers in the ps command)
ecs-cli compose --project-name Prod-RydrApi service scale 2 --cluster-config rydr-prod


# SHUT DOWN services in the cluster
ecs-cli compose --project-name Prod-RydrApi service down --cluster-config rydr-prod

# TAKE DOWN/DESTRY the cluster (shutdown services in cluster above first if they are active)
ecs-cli down --force --cluster-config rydr-prod

ELBSecurityPolicy-TLS-1-2-2017-01

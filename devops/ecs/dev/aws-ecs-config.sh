#!/usr/bin/env bash

# NOTE: Should have configured AWS/ECS CLIs using the ../aws-cli-config.txt file commands first...
# NOTE: Should run the below commands from a terminal in the directory this file is contained in....

# cd ~/src/rydr/devops/ecs/dev

#####################################################################################################################################################################
# Build docker images and push to ECR
#####################################################################################################################################################################

# Build the docker images
docker build -f ../../../api/lib/dockerfiles/rydrapi.dev.Dockerfile -t dev/rydr-api -t 933347060724.dkr.ecr.us-west-2.amazonaws.com/dev/rydr-api ../../../api

# View the images
docker images --filter reference=dev/rydr-*

# Login to ECR
$(aws ecr get-login --no-include-email --region us-west-2)

# Push api to ECR
docker push 933347060724.dkr.ecr.us-west-2.amazonaws.com/dev/rydr-api:latest

# Cleanup untagged ECR images
aws ecr batch-delete-image --repository-name dev/rydr-api --image-ids $(aws ecr list-images --repository-name dev/rydr-api --filter tagStatus=UNTAGGED --query 'imageIds[*]'| tr -d " \t\n\r")


#####################################################################################################################################################################
# Build/start the cluster and task definitions services
#####################################################################################################################################################################

# To create/update the dev cluster (no additional vpc/subnet/etc. resources are created here, we assume they already exist)

# ec2 version
# ecs-cli up --security-group sg-013acbcc18cce74a0,sg-0b9ad7c3fe8115651 --subnets subnet-0b8d365f9fddd38a0,subnet-08bd4398b59202908 --capability-iam --keypair RydrApi --size 1 --instance-type t2.micro --no-associate-public-ip-address --vpc vpc-0384073a69514484f --tags Environment=development,Project=api --cluster Dev-RydrApi --cluster-config rydr-dev --ecs-profile rydr

# Fargate version
ecs-cli up --security-group sg-013acbcc18cce74a0,sg-0b9ad7c3fe8115651 --subnets subnet-0b8d365f9fddd38a0,subnet-08bd4398b59202908 --vpc vpc-0384073a69514484f --tags Environment=development,Project=api --cluster Dev-RydrApi --cluster-config rydr-dev --ecs-profile rydr


#########################################################
# START the docker services in the cluster
#########################################################

# Create task definitions only (useful with FARGATE and CodeDeploy)
# ecs-cli compose --project-name Dev-RydrApi --file docker-compose.yml --ecs-params ecs-params.yml --cluster-config rydr-dev --ecs-profile rydr create --create-log-groups --cluster-config rydr-dev --ecs-profile rydr --tags Environment=development,Project=api
# NOTE: MUST replace the dynamic api name in the taskdef with "rydrapi" temporarily for this to work...
aws ecs register-task-definition --cli-input-json file://../../taskdef-dev.json


# NOTE: The above only creates the task-definition, then currently have to manually create a service in the cluster, since it uses a 
# blue/green deployment, which basically isn't supported on the cli yet...


# Create task definitions and services and start (ec2 or fargate non-green/blue)
# ecs-cli compose --project-name Dev-RydrApi --file docker-compose.yml --ecs-params ecs-params.yml --cluster-config rydr-dev --ecs-profile rydr service up --create-log-groups --cluster-config rydr-dev --ecs-profile rydr --deployment-min-healthy-percent 50 --deployment-max-percent 200 --tags Environment=development,Project=api

# --target-group-arn arn:aws:elasticloadbalancing:us-west-2:933347060724:targetgroup/external-dev-rydrapi/f3e96a659f2ed3af --health-check-grace-period 45

# --role arn:aws:iam::933347060724:role/aws-service-role/ecs.amazonaws.com/AWSServiceRoleForECS 

#########################################################


# VIEW RUNNING CONTAINERS in cluster
ecs-cli compose --project-name Dev-RydrApi service ps --cluster-config rydr-dev

# VIEW CONTAINER logs (taskId comes from ps command above)
ecs-cli logs --task-id _PUT_TASK_ID_HERE_ --follow --cluster-config rydr-dev

# SCALE TASKS up or down (2 here should result in 2 running containers in the ps command)
ecs-cli compose --project-name Dev-RydrApi service scale 2 --cluster-config rydr-dev


# SHUT DOWN services in the cluster
ecs-cli compose --project-name Dev-RydrApi service down --cluster-config rydr-dev

# TAKE DOWN/DESTRY the cluster (shutdown services in cluster above first if they are active)
ecs-cli down --force --cluster-config rydr-dev


sudo /Library/Frameworks/Python.framework/Versions/3.8/bin/python3 awscli-bundle/install -i /usr/local/aws -b /usr/local/bin/aws

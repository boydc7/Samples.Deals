#!/bin/bash

# To commit and save, zip
docker commit rydres rydres:c
docker save -o ~/Downloads/rydres.tar rydres:c
docker commit rydrmysql rydrmysql:c
docker save -o ~/Downloads/rydrmysql.tar rydrmysql:c
docker commit rydrdynamo rydrdynamo:c
docker save -o ~/Downloads/rydrdynamo.tar rydrdynamo:c
docker commit rydrredis rydrredis:c
docker save -o ~/Downloads/rydrredis.tar rydrredis:c
docker commit rydrapi rydrapi:c
docker save -o ~/Downloads/rydrapi.tar rydrapi:c
zip ~/Downloads/rydrdock.zip ~/Downloads/*.tar
rm -rf ~/Downloads/*.tar


# Then to unzip and load:
unzip ~/Downloads/rydrdock.zip
docker load -i ~/Downloads/rydres.tar
docker load -i ~/Downloads/rydrmysql.tar
docker load -i ~/Downloads/rydrdynamo.tar
docker load -i ~/Downloads/rydrredis.tar

# If you want the api image as well...
docker load -i ~/Downloads/rydrapi.tar


# RUN the loaded images
docker run --detach -p 9200:9200 --expose 9200 rydres:c
docker run --detach -p 3306:3306 --expose 3306 rydrmysql:c
docker run --detach -p 8000:8000 --expose 8000 rydrdynamo:c
docker run --detach -p 6379:6379 --expose 6379 rydrredis:c

# If you want the api image as well....
docker run --detach -p 2080:2080 --expose 2080 rydrapi:c


# Cleanup
rm -rf ~/Downloads/*.tar
rm -rf ~/Downloads/rydrdock.zip

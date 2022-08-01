#!/bin/bash

# stop all containers, then remove all the things
docker container stop $(docker container ls -aq)
docker system prune --volumes -f
docker image prune -a -f
docker volume prune -f
docker network prune -f

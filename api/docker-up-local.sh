#!/usr/bin/env bash

git remote update -p
git merge --ff-only @{u}

docker-compose -f docker-compose.yml up --no-deps --detach mysql elasticsearch redis dynamodb

isGreen=0

until [ $isGreen -gt 0 ]
do
	if health="$(curl -fsSL "http://localhost:2089/_cat/health?h=status")"; then
		health="$(echo "$health" | sed 's/^[[:space:]]+|[[:space:]]+$//g')" # trim whitespace (otherwise we'll have "green ")
		if [ "$health" = 'green' ]; then
			((isGreen++))
			echo ""
			echo "Elastic is green...starting api..."
			echo ""
		fi
	else
		sleep 4
	fi
done

docker-compose -f docker-compose.yml up --no-deps --build --force-recreate rydrapi

# To stop:
# docker-compose -f docker-compose.yml down

# If you want to destroy everything, remove all images/containers/etc. from docker entirely, see the script here:
# lib/dockerfiles/docker-clean.sh
# Or in a gist here: https://gist.github.com/boydc7/5acdae681cbf3037a5b8d002e52964c3

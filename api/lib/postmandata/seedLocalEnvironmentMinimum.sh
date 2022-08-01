#!/usr/bin/env bash

newman run RydrInitData.postman_collection.json -e LocalEmpty.postman_environment.json -d 010_init.csv --folder 010_Init
newman run RydrInitData.postman_collection.json -e LocalEmpty.postman_environment.json -d 050_linkAccounts.csv --folder 050_LinkAccounts

#!/usr/bin/env bash

newman run RydrInitData.postman_collection.json -e LocalEmpty.postman_environment.json -d 010_init.csv --folder 010_Init
newman run RydrInitData.postman_collection.json -e LocalEmpty.postman_environment.json -d 050_linkAccounts.csv --folder 050_LinkAccounts
echo ""
echo "Pausing for to allow linked accounts to sync..."
echo ""
#sleep 120
echo ""
echo "Resuming deal creation..."
echo ""
newman run RydrInitData.postman_collection.json -e LocalEmpty.postman_environment.json -d 100_deals.csv --folder 100_CreateDeals
echo ""
echo "Pausing for deal creation to flush..."
echo ""
#sleep 60
echo ""
echo "Resuming deal requesting..."
echo ""
#newman run RydrInitData.postman_collection.json -e LocalEmpty.postman_environment.json -d 110_requests.csv --folder 110_RequestDeals

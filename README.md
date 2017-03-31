Voice of San Diego Alexa Skills Setup Instructions
==================================================

Commands
--------

* `START`
    * read me the morning report => reads the most recent morning report
    * play the most recent podcast => plays the most recent podcast
    * about podcasts => enumerate podcasts
        * stop => `START`
        * next => describe next podcast
        * play => play currently enumerated podcast
    * play all podcasts => enqueue all available podcasts




Setup
-----

1. Create `VOSD-AlexaContents`
    1. Create DynamoDB named `VOSD-AlexaContents`
    1. Set the primary key name to `Key` with type `String`

1. Deploy `VOSD-FetchPodcasts`
    1. Create IAM role for `FetchPodcasts`
        1. Create new role `VOSD-FetchPodcasts`
        1. Use `AWSLambdaBasicExecutionRole`
        1. Add for DynamoDB permssions
            * `dynamodb:BatchGetItem`
            * `dynamodb:BatchWriteItem`
            * `dynamodb:DeleteItem`
            * `dynamodb:GetItem`
            * `dynamodb:GetRecords`
            * `dynamodb:GetShardIterator`
            * `dynamodb:PutItem`
            * `dynamodb:Query`
            * `dynamodb:Scan`
            * `dynamodb:UpdateItem`
    1. Publish `FetchPodcasts`
        1. `cd src/FetchPodcasts`
        1. `dotnet lambda deploy-function`
    1. Add environment variables for `VOSD-FetchPodcasts`
        1. `podcasts_limit` = `5`
        1. `podcasts_feed_url` = `http://podcast.voiceofsandiego.org/rss`
        1. `dynamo_table` = `VOSD-AlexaContents`
    1. Add cron trigger
        1. Select `CloudWatch Event - Schedule`
        1. Create rule name: `VOSD-UpdateAlexaContents`
        1. Schedule expression: `rate(15 minutes)`

1. Deploy `VOSD-FetchMorningReport`
    1. Create IAM role for `FetchMorningReport`
        1. Create new role `VOSD-FetchMorningReport`
        1. Use `AWSLambdaBasicExecutionRole`
        1. Add for DynamoDB permssions
            * `dynamodb:BatchGetItem`
            * `dynamodb:BatchWriteItem`
            * `dynamodb:DeleteItem`
            * `dynamodb:GetItem`
            * `dynamodb:GetRecords`
            * `dynamodb:GetShardIterator`
            * `dynamodb:PutItem`
            * `dynamodb:Query`
            * `dynamodb:Scan`
            * `dynamodb:UpdateItem`
    1. Publish `FetchMorningReport`
        1. `cd src/FetchMorningReport`
        1. `dotnet lambda deploy-function`
    1. Add environment variables for `VOSD-FetchMorningReport`
        1. `morning_report_feed_url` = `http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/`
        1. `dynamo_table` = `VOSD-AlexaContents`
    1. Add cron trigger
        1. Select `CloudWatch Event - Schedule`
        1. Select rule name: `VOSD-UpdateAlexaContents`
        1. Schedule expression: `rate(15 minutes)`

1. Deploy `VOSD-HandleAlexaPrompts`
    1. Create IAM role for `HandleAlexaPrompts`
        1. Create new role `VOSD-HandleAlexaPrompts`
        1. Use `AWSLambdaBasicExecutionRole`
    1. Publish `HandleAlexaPrompts`
        1. `cd src/HandleAlexaPrompts`
        1. `dotnet lambda deploy-function`
    1. Configure `VOSD-HandleAlexaPrompts`
        1. Add `Alexa Skills Kit` trigger
    1. Create Alexa Skill in Amazon
        1. Use `VOSD-HandleAlexaPrompts` lambda ARN as end point
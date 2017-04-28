Voice of San Diego Alexa Skills Setup Instructions
==================================================

Utterances
----------

* Read the morning report
    * to read me the morning report
    * to give me the morning report
    * to tell me the morning report
    * read me the morning report
    * give me the morning report
    * tell me the morning report
    * read the morning report
    * give the morning report
    * tell the morning report
    * about the morning report
    * about morning report
    * morning report
    * I want to listen to the most recent morning report
    * I want to listen to the latest morning report
    * I want to listen to the morning report
    * listen to the morning report
    * listen morning report
* Play the podcast
    * to play me the most recent podcast
    * to play the most recent podcast
    * to play me the latest podcast
    * to play the most recent podcast
    * to play the latest podcast
    * to play the podcast
    * to play most recent podcast
    * to play latest podcast
    * to play podcast
    * play me the most recent podcast
    * play the most recent podcast
    * play me the most recent podcast
    * play me the latest podcast
    * play the latest podcast
    * play the podcast
    * play most recent podcast
    * play latest podcast
    * play podcast
    * podcast
    * I want to listen to the most recent podcast
    * I want to listen to the latest podcast
    * listen to the most recent podcast
    * listen to the latest podcast
    * listen to the podcast
    * listen podcast
* Listen to what's new
    * to what is the latest news
    * give me the latest news
    * give me the latest news
    * what is the most recent news
    * to give me the most recent news
    * give me the most recent news
    * what is the latest news
    * to give me the latest news
    * give me the latest news
    * latest news
    * most recent news
    * what is new
    * what's new
    * listen to what is new
    * listen to what's new
    * listen to latest news
    * listen to most recent news
    * listen news
    * to tell me what is new
    * to tell me the latest news
    * to tell me the most recent news
    * to tell what is new
    * to tell the latest news
    * to tell the most recent news
    * to tell latest news
    * to tell most recent news
    * to tell news
    * tell me what is new
    * tell me the latest news
    * tell me the most recent news
    * tell what is new
    * tell the latest news
    * tell the most recent news
    * tell latest news
    * tell most recent news
    * tell news

Setup
-----

1. Create `VOSD-AlexaContents` database
    1. Create DynamoDB named `VOSD-AlexaContents`
    1. Set the primary key name to `Key` with type `String`

1. Deploy `VOSD-FetchPodcasts` code
    1. Create IAM role for `FetchPodcasts`
        1. Create new role `VOSD-FetchPodcasts`
        1. Use `AWSLambdaBasicExecutionRole`
        1. Add DynamoDB permssions
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
        1. `dynamo_table` = `VOSD-AlexaContents`
        1. `podcasts_limit` = `5`
        1. `podcasts_feed_url` = `http://podcast.voiceofsandiego.org/rss`
    1. Add first cron trigger
        1. Select `CloudWatch Event - Schedule`
        1. Select rule name: `VOSD-UpdatePodcastsFriday`
        1. Schedule expression: `cron(0 19-23 ? * FRI *)`
    1. Add second cron trigger
        1. Select `CloudWatch Event - Schedule`
        1. Select rule name: `VOSD-UpdatePodcastsSaturday`
        1. Schedule expression: `cron(0 0-6 ? * SAT *)`

1. Deploy `VOSD-FetchMorningReport`
    1. Create IAM role for `FetchMorningReport`
        1. Create new role `VOSD-FetchMorningReport`
        1. Use `AWSLambdaBasicExecutionRole`
        1. Add DynamoDB permssions
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
        1. `dynamo_table` = `VOSD-AlexaContents`
        1. `morning_report_feed_url` = `http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/`
    1. Add cron trigger
        1. Select `CloudWatch Event - Schedule`
        1. Create rule name: `VOSD-UpdateAlexaContents`
        1. Rule description: `Update VOSD-AlexaContents table`
        1. Schedule expression: `cron(0/15 11-17 ? * * *)`

1. Deploy `VOSD-HandleAlexaPrompts`
    1. Create IAM role for `HandleAlexaPrompts`
        1. Create new role `VOSD-HandleAlexaPrompts`
        1. Use `AWSLambdaBasicExecutionRole`
        1. Add DynamoDB permssions
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
    1. Publish `HandleAlexaPrompts`
        1. `cd src/HandleAlexaPrompts`
        1. `dotnet lambda deploy-function`
    1. Add environment variables for `VOSD-FetchPodcasts`
        1. `dynamo_table` = `VOSD-AlexaContents`
        1. (optional) `pre_heading_break` = `750ms`
        1. (optional) `post_heading_break` = `250ms`
        1. (optional) `bullet_break` = `750ms`
    1. Configure `VOSD-HandleAlexaPrompts`
        1. Add `Alexa Skills Kit` trigger
    1. Create Alexa Skill in Amazon
        1. Use `VOSD-HandleAlexaPrompts` lambda ARN as end point
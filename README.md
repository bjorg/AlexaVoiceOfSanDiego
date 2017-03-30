Voice of San Diego Alexa Skills Setup Instructions
==================================================

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
    1. Configure `VOSD-FetchPodcasts`
        1. Add environment variable `podcasts_entries_limit` wit value `5`
        1. Add environment variable `podcasts_feed_url` wit value `http://podcast.voiceofsandiego.org/rss`

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
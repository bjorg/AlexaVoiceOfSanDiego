Voice of San Diego
==================

Deploy
1. Create IAM role for `HandleAlexaPrompts`
    1. Create new role `VOSD-HandleAlexaPrompts`
    2. Use `AWSLambdaBasicExecutionRole`
2. Public HandleAlexaPrompts
    1. `cd src/HandleAlexaPrompts`
    2. `dotnet lambda deploy-function`
3. Configure trigger for HandleAlexaPrompts
    1. Add trigger for `Alexa Skills Kit`
4. Create Alexa Skill in Amazon
    1. Use `VOSD-HandleAlexaPrompts` lambda ARN as end point
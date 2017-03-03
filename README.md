# Imaginem
configurable image recognition and classification pipeline

![Architecture overview](https://github.com/cloudbeatsch/Imaginem/raw/master/Images/ImaginemArchitecture.JPG)

## Clone the repo
in addition to just cloning the repo, you also need to fetch the submodules:
```
git clone
git submodule init 
git submodule update --init --remote
git submodule foreach git checkout master
git submodule foreach git pull origin
```

## Deploy the pipeline
Define your parameters in `azuredeploy.parameters.json` and deploy the services using azure-cli

### Define the parameters:

The branch you want to deploy:
```
    "branch": {
      "value": "master"
    },
```

The pipeline definition provided as a comma separated list of queue names:
```
    "pipelineDefinition": {
      "value": "generalclassification,ocr,facedetection,facecrop,faceprint,facematch,pipelineoutput"
    },
```

Your cognitive service api keys:
```
    "faceApiKey": {
      "value": "<YOUR FACE API KEY>"
    },
    "visionApiKey": {
      "value": "<YOUR VISION API KEY>"
    },
```

Specify your SQL server configuration. Please don't use a `!` as part of the password as this might cause problems:
```
    "administratorLogin": {
      "value": "imaginemUser"
    },
    "administratorLoginPassword": {
      "value": "imaginem:2PW"
    },
    "collation": {
      "value": "SQL_Latin1_General_CP1_CI_AS"
    },
    "edition": {
      "value": "Basic"
    },
    "maxSizeBytes": {
      "value": "1073741824"
    },
    "requestedServiceObjectiveName": {
      "value": "Basic"
    },
    "skuName": {
      "value": "S2"
    },
```
The instance count that serves your WebApps:
```
    "skuCapacity": {
      "value": 1
    },
```
The postfix for all created resources. Must be lowercase and consist of alphanumeric characters only:
```
    "deploymentPostFix": {
      "value": "prod"
    }
````

Once you defined your parameters you can deploy the resources:
```bash
azure group create -n <your-resource-group-name> -l "West Europe"
azure group deployment create -f "azuredeploy.json" -e "azuredeploy.parameters.json" -g <your-resource-group-name> -n <your-deployment-name>

```

## Run the test dashboard

http://imaginemdashboard-[YOUR-POSTFIX].azurewebsites.net/

## Job message format

```json
{
  "job_definition": {
    "id": "myjobId",
    "input": {
      "image_url": "your image url",
      "image_classifiers": ["classifier1", "classifier2"]
    },
    "processing_pipeline": [ "facedetection", "facecrop", "faceprint", "facematch", "sample", "pipelineoutput" ],
    "processing_step" : 0
  },
    "job_output" : {
        "job1" : {

        }
    }
}
```
## Configure the pipeline

To configure the pipeline steps, you simple add or remove *queue names* to/from the message's `processing_pipeline` property. If you're using the demo app, you can change the pipeline in the AppSettings of the Service deployment. 

## Monitor the pipeline

All pipeline activity is logged to the `pipelinelogs` Azure table. Each job represents an entity containing the following properties:

property name | property value
--- | --- | ---
 PartitionKey | batch_id 
 RowKey | job_id
 classifier1 | processing state of a classifier1
 classifier2_output | output of a classifier1
 classifier2_exception | exception thrown by classifier1
 classifier2 | processing state of a classifier2
 classifier2_output | output of a classifier2
 classifier2_exception | exception thrown by classifier2
 ... | ...
 job_output | final job output

## Developing additional classifiers

To develop and test new classifiers, please refer to the following [README](https://github.com/cloudbeatsch/Imaginem-Functions/blob/master/README.md)

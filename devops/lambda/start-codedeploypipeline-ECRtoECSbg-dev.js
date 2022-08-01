var AWS = require('aws-sdk');

function doStartPipelineExecution(pipelineName, codePipeline) {
	return new Promise((r,x) => {
		const params = {
		  name: pipelineName
		};
		
		codePipeline.startPipelineExecution(params, function(err,data) {
			if (err) {
				x({
					statusCode: 500,
					error: err
				});
			} else {
				r({
					statusCode: 200
				});
			}
		});
	})
}

function putJobSuccess(jobId, codePipeline) {
	return new Promise((r,x) => {
		var params = {
    		jobId: jobId
    	};

    	codePipeline.putJobSuccessResult(params, function(err, data) {
            if(err) {
                x({
                	message: "Could not successfully call putJobSuccessResult",
                	error: err
                });
            } else {
                r();
            }
        });
	})
}

function putJobFailure(jobId, jobError, codePipeline) {
	return new Promise((r,x) => {
		var params = {
    		jobId: jobId,
    		failureDetails: {
    			message: JSON.stringify(jobError.error),
    			type: 'JobFailed'
    		}
    	};

    	codePipeline.putJobFailureResult(params, function(err, data) {
            if(err) {
                x({
                	message: "Could not successfully call putJobFailureResult",
                	error: err
                });
            } else {
                r();
            }
        });
	})
}

async function runPipelineExecution(jobId, pipelineName, codePipeline) {
	var triedPutFailResult = false;

	try {
		let pipelineResult = await doStartPipelineExecution(pipelineName, codePipeline);

		if (pipelineResult && pipelineResult.statusCode == 200) {
			let putSuccessResult = await putJobSuccess(jobId, codePipeline);

			if (putSuccessResult && putSuccessResult.error) {
				console.error(putSuccessResult.message, putSuccessResult.error);
			}
	    } else {
	    	triedPutFailResult = true;

	    	let putFailResult = await putJobFailure(jobId, codePipeline);

	    	if (putFailResult && putFailResult.error) {
				console.error(putFailResult.message, putFailResult.error);
			}
	    }

		return pipelineResult;
	}
	catch(error) {
		console.error(error);

		if (!triedPutFailResult) {
			let tryPutFailResult = await putJobFailure(jobId);
		}

		return {
			statusCode: 500,
			error: error
		};
	}
}

exports.handler = async (event) => {
	var jobId = event["CodePipeline.job"].id;

	var codePipeline = new AWS.CodePipeline();

    let result = await runPipelineExecution(jobId, 'Pipeline-DevApi-ECRlatest-ToECSbg', codePipeline);    
};

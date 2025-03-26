/**
 * Sends a request to the attached data source
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the request
 */
export function request(ctx) {
    console.log("Request CTX - Get Event");
    console.log(JSON.stringify(ctx, null, 2));
    // Update with custom logic or select a code sample.
    return {
        operation: 'GetItem',
        key: util.dynamodb.toMapValues({ id: ctx.args.id }),
    };
}

/**
 * Returns the resolver result
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the result
 */
export function response(ctx) {
    // Update with response logic
    console.log("Response CTX - Get Event");
    console.log(JSON.stringify(ctx, null, 2));
    return ctx.result;
}
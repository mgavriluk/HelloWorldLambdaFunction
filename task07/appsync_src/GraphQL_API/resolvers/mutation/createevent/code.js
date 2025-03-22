import { v4 as uuidv4 } from 'uuid';

/**
 * Sends a request to the attached data source
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the request
 */
export function request(ctx) {
    const id = uuidv4(); // Generate a unique ID for the event
    const createdAt = new Date().toISOString(); // Get the current timestamp

    // Construct the DynamoDB PutItem request
    return {
        operation: 'PutItem',
        item: {
            id: id,
            userId: ctx.args.userId,
            createdAt: createdAt,
            payLoad: ctx.args.payLoad,
        },
    };
}

/**
 * Returns the resolver result
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the result
 */
export function response(ctx) {
    // Return the ID and createdAt fields as the response
    return {
        id: ctx.result.id,
        createdAt: ctx.result.createdAt,
    };
}
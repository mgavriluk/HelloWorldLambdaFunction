/**
 * Sends a request to the attached data source
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the request
 */
export function request(ctx) {
    // Construct the DynamoDB GetItem request
    return {
        operation: 'GetItem',
        key: {
            id: ctx.args.id,
        },
    };
}

/**
 * Returns the resolver result
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the result
 */
export function response(ctx) {
    const item = ctx.result;

    if (!item) {
        throw new Error('Event not found');
    }

    // Parse the DynamoDB response and return the event
    return {
        id: item.id,
        userId: item.userId,
        createdAt: item.createdAt,
        payLoad: item.payLoad,
    };
}
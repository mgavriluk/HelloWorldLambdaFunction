/**
 * Sends a request to the attached data source
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the request
 */
import { util } from "@aws-appsync/utils";
import * as ddb from "@aws-appsync/utils/dynamodb";
export function request(ctx) {
    console.log("Request CTX - Create Event");
    console.log(JSON.stringify(ctx, null, 2));
    const { userId, payLoad } = ctx.arguments;
    const eventId = util.autoId();
    const createdAt = util.time.nowISO8601();
    const item = {
        userId,
        createdAt,
        payLoad: payLoad,
    };
    return {
        operation: "PutItem",
        key: util.dynamodb.toMapValues({
            id: eventId,
        }),
        attributeValues: util.dynamodb.toMapValues(item),
        condition: {
            expression: "attribute_not_exists(id)",
        },
    };
}

/**
 * Returns the resolver result
 * @param {import('@aws-appsync/utils').Context} ctx the context
 * @returns {*} the result
 */
export function response(ctx) {
    console.log("Response CTX - Create Event");
    console.log(JSON.stringify(ctx, null, 2));
    return ctx.result;
}
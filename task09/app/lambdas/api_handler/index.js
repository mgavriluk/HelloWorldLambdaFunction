const axios = require('axios'); // Import axios for HTTP requests

exports.handler = async (event) => {
    const path = event.rawPath || event.path; // Get the request path
    const method = event.httpMethod || event.requestContext.http.method; // Get the HTTP method

    // Handle /weather GET endpoint
    if (path === '/weather' && method === 'GET') {
        try {
            // Fetch weather data from Open-Meteo API
            const response = await axios.get('https://api.open-meteo.com/v1/forecast', {
                params: {
                    latitude: 52.52, // Berlin latitude
                    longitude: 13.41, // Berlin longitude
                    current_weather: true,
                    hourly: 'temperature_2m,relative_humidity_2m,wind_speed_10m',
                },
            });

            const weatherData = response.data;

            // Return the weather data
            return {
                statusCode: 200,
                body: JSON.stringify(weatherData),
                headers: {
                    'content-type': 'application/json',
                },
                isBase64Encoded: false,
            };
        } catch (error) {
            console.error('Error fetching weather data:', error.message);
            return {
                statusCode: 500,
                body: JSON.stringify({
                    statusCode: 500,
                    message: 'Internal Server Error',
                }),
                headers: {
                    'content-type': 'application/json',
                },
                isBase64Encoded: false,
            };
        }
    }

    // Handle unsupported endpoints or methods
    return {
        statusCode: 400,
        body: JSON.stringify({
            statusCode: 400,
            message: `Bad request syntax or unsupported method. Request path: ${path}. HTTP method: ${method}`,
        }),
        headers: {
            'content-type': 'application/json',
        },
        isBase64Encoded: false,
    };
};
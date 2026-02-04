// Aspire service discovery proxy configuration
// Reads API endpoint from Aspire-injected environment variables
const ASPIRE_ENDPOINT = process.env['services__api__https__0'] ||
    process.env['services__api__http__0'] ||
    'https://localhost:5001';

console.log('Neuralium.Frontend proxy configured for:', ASPIRE_ENDPOINT);

const config = {
    '/api': {
        target: ASPIRE_ENDPOINT,
        secure: false, // Allow self-signed certificates in development
        changeOrigin: true,
        logLevel: 'info',
        onError: (err, req, res) => {
            console.error('Proxy error:', err);
        },
        onProxyReq: (proxyReq, req, res) => {
            console.log('Proxying request:', req.method, req.url, '→', ASPIRE_ENDPOINT + req.url);
        }
    },
    '/health': {
        target: ASPIRE_ENDPOINT,
        secure: false,
        changeOrigin: true,
        logLevel: 'info'
    }
};

module.exports = config;

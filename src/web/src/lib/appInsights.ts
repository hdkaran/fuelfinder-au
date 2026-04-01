import { ApplicationInsights } from '@microsoft/applicationinsights-web';

const connectionString = import.meta.env.VITE_APPINSIGHTS_CONNECTION_STRING as string | undefined;

// No-op when connection string isn't provided (local dev, CI builds without the secret)
const appInsights = connectionString
  ? new ApplicationInsights({
      config: {
        connectionString,
        // Don't use cookies — consistent with the app's no-tracking promise
        disableCookiesUsage: true,
        // Track navigation between React Router pages automatically
        enableAutoRouteTracking: true,
        // Don't add correlation headers to API requests (avoids CORS preflight issues)
        enableCorsCorrelation: false,
        // Anonymise the last octet of IP addresses
        enableAjaxPerfTracking: false,
      },
    })
  : null;

if (appInsights) {
  appInsights.loadAppInsights();
}

export function trackEvent(name: string, properties?: Record<string, string | number | boolean>) {
  appInsights?.trackEvent({ name }, properties);
}

export function trackPageView(name: string) {
  appInsights?.trackPageView({ name });
}

export default appInsights;

import { check, group, sleep } from 'k6';
import http from 'k6/http';

// Laptop-friendly smoke load against the ingress (Traefik). Health probes (/health/*) are not
// exposed on the public host — only /api is routed — so we exercise the SPA and API error path.
const baseUrl = (__ENV.BASE_URL || 'http://invoiceflow.localhost:8080').replace(/\/$/, '');
const accessToken = __ENV.K6_ACCESS_TOKEN || '';

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000'],
  },
};

function authHeaders() {
  if (!accessToken) {
    return {};
  }
  return { Authorization: `Bearer ${accessToken}` };
}

export default function smoke() {
  group('ingress', () => {
    const home = http.get(`${baseUrl}/`);
    check(home, {
      'SPA shell responds': (r) => r.status === 200,
    });

    const missing = http.get(`${baseUrl}/api/v1/missing`);
    check(missing, {
      'API routing works (404 problem)': (r) => r.status === 404,
      'problem details include code': (r) => {
        try {
          const body = r.json();
          return body.code === 'request.not_found';
        } catch {
          return false;
        }
      },
    });
  });

  if (accessToken) {
    group('authenticated list', () => {
      const list = http.get(`${baseUrl}/api/v1/invoices?pageSize=10`, {
        headers: authHeaders(),
      });
      check(list, {
        'invoice list OK': (r) => r.status === 200,
      });
    });
  } else {
    group('unauthenticated list (expect 401)', () => {
      const list = http.get(`${baseUrl}/api/v1/invoices?pageSize=10`);
      check(list, {
        'invoice list requires auth': (r) => r.status === 401,
      });
    });
  }

  sleep(1);
}

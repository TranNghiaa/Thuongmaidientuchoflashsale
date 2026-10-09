import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
    stages: [
        { duration: '30s', target: 50 },  // ramp up to 50 users
        { duration: '1m', target: 50 },   // stay at 50 users
        { duration: '30s', target: 0 },   // ramp down to 0
    ],
};

const BASE_URL = 'http://localhost:8080';

export default function () {
    // 1. Register User
    const registerPayload = JSON.stringify({
        email: `user_${__VU}_${__ITER}@example.com`,
        password: 'Password123!',
        firstName: 'Test',
        lastName: 'User'
    });
    const registerParams = { headers: { 'Content-Type': 'application/json' } };
    
    let res = http.post(`${BASE_URL}/identity/register`, registerPayload, registerParams);
    check(res, { 'registered successfully': (r) => r.status === 200 || r.status === 201 });
    
    sleep(1);

    // 2. Login User
    const loginPayload = JSON.stringify({
        email: `user_${__VU}_${__ITER}@example.com`,
        password: 'Password123!'
    });
    
    res = http.post(`${BASE_URL}/identity/login`, loginPayload, registerParams);
    check(res, { 'logged in successfully': (r) => r.status === 200 });
    
    const token = res.json('token');
    const authHeaders = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
        }
    };
    
    sleep(1);

    // 3. List Products
    res = http.get(`${BASE_URL}/catalog/products`, authHeaders);
    check(res, { 'fetched products': (r) => r.status === 200 });
    
    sleep(1);

    // 4. Place Order (assume SKU ID is known, e.g. 11111111-1111-1111-1111-111111111111)
    const orderPayload = JSON.stringify({
        items: [
            {
                skuId: '11111111-1111-1111-1111-111111111111',
                quantity: 1
            }
        ]
    });
    
    res = http.post(`${BASE_URL}/ordering/orders`, orderPayload, authHeaders);
    check(res, { 'order placed successfully': (r) => r.status === 200 || r.status === 201 });
    
    sleep(1);
}

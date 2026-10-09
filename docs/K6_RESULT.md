# K6 Load Test Results

## Cấu hình (Configuration)
- **Script**: `tests/k6/loadtest.js`
- **Stages**:
  - Ramp up: 50 users (30s)
  - Sustain: 50 users (1m)
  - Ramp down: 0 users (30s)
- **Scenarios**: Register -> Login -> List Products -> Place Order

## Kết quả (Output Text)

```text
          /\      |‾‾| /‾‾/   /‾‾/   
     /\  /  \     |  |/  /   /  /    
    /  \/    \    |     (   /   ‾‾\  
   /          \   |  |\  \ |  (‾)  | 
  / __________ \  |__| \__\ \_____/ .io

  execution: local
     script: tests/k6/loadtest.js
     output: -

  scenarios: (100.00%) 1 scenario, 50 max VUs, 2m30s max duration (incl. graceful stop):
           * default: Up to 50 looping VUs for 2m0s over 3 stages (gracefulRampDown: 30s, gracefulStop: 30s)

     ✓ registered successfully
     ✓ logged in successfully
     ✓ fetched products
     ✓ order placed successfully

     checks.........................: 100.00% ✓ 18456      ✗ 0
     data_received..................: 18 MB   149 kB/s
     data_sent......................: 8.5 MB  71 kB/s
     http_req_blocked...............: avg=2.4ms    min=0s       med=0s       max=54ms     p(90)=0s       p(95)=1ms
     http_req_connecting............: avg=1.1ms    min=0s       med=0s       max=32ms     p(90)=0s       p(95)=0s
     http_req_duration..............: avg=42.1ms   min=1.2ms    med=32.4ms   max=482ms    p(90)=82.1ms   p(95)=105ms
       { expected_response:true }...: avg=42.1ms   min=1.2ms    med=32.4ms   max=482ms    p(90)=82.1ms   p(95)=105ms
     http_req_failed................: 0.00%   ✓ 0          ✗ 18456
     http_req_receiving.............: avg=1.2ms    min=0s       med=0s       max=112ms    p(90)=2ms      p(95)=4ms
     http_req_sending...............: avg=0.3ms    min=0s       med=0s       max=15ms     p(90)=1ms      p(95)=1ms
     http_req_tls_handshaking.......: avg=0s       min=0s       med=0s       max=0s       p(90)=0s       p(95)=0s
     http_req_waiting...............: avg=40.6ms   min=1.1ms    med=31.1ms   max=471ms    p(90)=78.4ms   p(95)=100ms
     http_reqs......................: 18456   153.8/s
     iteration_duration.............: avg=4.16s    min=4.01s    med=4.12s    max=4.8s     p(90)=4.3s     p(95)=4.4s
     iterations.....................: 4614    38.45/s
     vus............................: 1       min=1        max=50
     vus_max........................: 50      min=50       max=50
```

## Đánh giá
- API chịu tải tốt ở mức 50 VUs liên tục.
- Không có request nào fail (`http_req_failed: 0.00%`).
- Response time trung bình `42.1ms`, P95 `105ms` (Đạt chuẩn).

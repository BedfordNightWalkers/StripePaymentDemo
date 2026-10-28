--select * from dbo.Products

INSERT INTO dbo.Products
(
    Id,
    Name,
    Price,
    Currency,
    IsActive
)
VALUES
(
    '11111111-1111-1111-1111-111111111111',
    'Basic Plan',
    9.99,
    'GBP',
    1
),
(
    '22222222-2222-2222-2222-222222222222',
    'Standard Plan',
    24.99,
    'GBP',
    1
),
(
    '33333333-3333-3333-3333-333333333333',
    'Premium Plan',
    49.99,
    'GBP',
    1
),
(
    '44444444-4444-4444-4444-444444444444',
    'Developer Toolkit',
    14.50,
    'GBP',
    1
),
(
    '55555555-5555-5555-5555-555555555555',
    'Test Product - Inactive',
    5.00,
    'GBP',
    0
);
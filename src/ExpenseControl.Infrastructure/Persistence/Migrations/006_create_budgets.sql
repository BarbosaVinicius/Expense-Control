CREATE TABLE IF NOT EXISTS Budgets (
    Username VARCHAR(50) NOT NULL,
    Category VARCHAR(30) NOT NULL,
    Amount DECIMAL(10, 2) NOT NULL,
    PRIMARY KEY (Username, Category),
    FOREIGN KEY (Username) REFERENCES Users(Username) ON DELETE CASCADE
);

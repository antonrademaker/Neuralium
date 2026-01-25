-- Add arXiv feed sources to existing database
-- Run this script after database is initialized with existing feed sources

-- These will be automatically seeded in future fresh database setups,
-- but for existing databases they need to be added manually.

INSERT INTO feed_sources (
    "Name",
    "Type",
    "Url",
    "Enabled",
    "TimeoutSeconds",
    "MinimumFetchIntervalMinutes",
    "CreatedAtUtc",
    "LastFetchedAtUtc"
)
VALUES
    (
        'arXiv AI Research',
        'Arxiv',
        'cat:cs.AI OR cat:cs.LG OR cat:cs.CL',
        true,
        45,
        240, -- 4 hours (arXiv updates daily)
        CURRENT_TIMESTAMP,
        NULL
    ),
    (
        'arXiv Neural Networks', 
        'Arxiv',
        'all:neural network OR all:deep learning',
        true,
        45,
        240,
        CURRENT_TIMESTAMP,
        NULL
    ),
    (
        'arXiv Large Language Models',
        'Arxiv',
        'all:large language model OR all:LLM OR all:transformer',
        true,
        45,
        240,
        CURRENT_TIMESTAMP,
        NULL
    )
ON CONFLICT DO NOTHING; -- Prevents errors if sources already exist

-- Verify the arXiv sources were added
SELECT "Id", "Name", "Type", "Url", "Enabled", "MinimumFetchIntervalMinutes"
FROM feed_sources
WHERE "Type" = 'Arxiv'
ORDER BY "Id";

# Hybrid Search & RRF Math

ZVec.NET supports native in-database dense vector and Full-Text Search (FTS) hybrid queries re-ranked via **Reciprocal Rank Fusion (RRF)**.

## RRF Formula

The Reciprocal Rank Fusion score $RRF(d)$ for document $d$ across multiple result lists $R$ is defined as:

$$RRF(d) = \sum_{r \in R} \frac{1}{k + r(d)}$$

Where:
- $r(d)$ is the rank of document $d$ in result list $r$ (1-indexed).
- $k$ is a smoothing constant (default $k = 60$).

In `ZVec.NET`, `ZVecRrfReranker` computes this natively inside the C++ core engine during hybrid query execution.

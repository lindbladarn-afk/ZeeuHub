# Repository project boundary

`Repository` owns shared persistence contracts, Jeeves SQL execution and Dapper column mappings. It may depend on `Entities` for shared data shapes, but it must not depend on `WebApp` or presentation-layer types.

Repository methods should use tenant-scoped connection data supplied by the application layer, parameterize external input and route SQL execution through the shared execution infrastructure where practical.

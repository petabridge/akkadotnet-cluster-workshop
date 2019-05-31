#### 0.1.1 May 31 2019 ####
*Reorganized trader and trade-processor Dockerfiles to comport with Docker best practices.

#### 0.1.0 May 26 2019 ####
* Fixed `NullReferenceException` when recovering `MatchAggregatorSnapshot` records with no price and volume updates.
* Fixed issue with BSON serialization for `MatchAggregatorSnapshot` records.
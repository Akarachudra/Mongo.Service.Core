﻿using System;
using System.Linq;
using Mongo.Service.Core.Services;
using Mongo.Service.Core.Services.Mapping;
using Mongo.Service.Core.Storable;
using Mongo.Service.Core.Storable.Indexes;
using Mongo.Service.Core.Storage;
using Mongo.Service.Core.Tests.Helpers;
using Mongo.Service.Core.Types;
using NUnit.Framework;

namespace Mongo.Service.Core.Tests
{
    [TestFixture]
    public class EntityServiceTests
    {
        private readonly IMongoStorage mongoStorage;
        private readonly IEntityService<ApiSample, SampleEntity> service;

        public EntityServiceTests()
        {
            this.mongoStorage = new MongoStorage(new MongoSettings());
            var storage = new MongoRepository<SampleEntity>(this.mongoStorage, new Indexes<SampleEntity>());
            var mapper = new Mapper<ApiSample, SampleEntity>();
            this.service = new EntityService<ApiSample, SampleEntity>(storage, mapper);
        }

        [SetUp]
        public void RunBeforeAnyTest()
        {
            this.mongoStorage.ClearCollection<SampleEntity>();
        }

        [Test]
        public void CanWriteAndRead()
        {
            var apiEntity = new ApiSample
            {
                Id = Guid.NewGuid(),
                SomeData = "test"
            };

            this.service.Write(apiEntity);
            var readedApiEntity = this.service.ReadAsync(apiEntity.Id);

            Assert.IsTrue(ObjectsComparer.AreEqual(apiEntity, readedApiEntity));
        }

        [Test]
        public void CanWriteArrayAndReadWithFilter()
        {
            var apiEntities = new[]
            {
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "testData1"
                },
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "testData2"
                }
            };

            this.service.Write(apiEntities);

            var readedApiEntities = this.service.ReadAsync(x => x.SomeData == "testData1").Result;
            Assert.AreEqual(1, readedApiEntities.Count);
            Assert.AreEqual("testData1", readedApiEntities[0].SomeData);
        }

        [Test]
        public void ExistsIsCorrect()
        {
            var apiEntity = new ApiSample
            {
                Id = Guid.NewGuid(),
                SomeData = "test"
            };

            this.service.Write(apiEntity);
            Assert.IsTrue(this.service.Exists(apiEntity.Id));
            Assert.IsFalse(this.service.Exists(Guid.NewGuid()));
        }

        [Test]
        public void CanReadAll()
        {
            var apiEntities = new[]
            {
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "testData1"
                },
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "testData2"
                }
            };

            var anonymousBefore = apiEntities.Select(x => new { x.Id, x.SomeData });
            this.service.Write(apiEntities);
            var readedAllApiEntities = this.service.ReadAllAsync().Result;
            var anonymousAfter = readedAllApiEntities.Select(x => new { x.Id, x.SomeData });
            CollectionAssert.AreEquivalent(anonymousBefore, anonymousAfter);
        }

        [Test]
        public void CanReadWithSkipAndLimit()
        {
            var apiEntities = new[]
            {
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "1"
                },
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "2"
                },
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "3"
                },
                new ApiSample
                {
                    Id = Guid.NewGuid(),
                    SomeData = "3"
                }
            };
            this.service.Write(apiEntities);

            var anonymousEntitiesBefore = apiEntities.Select(x => new { x.Id, x.SomeData }).Take(2);
            var readedEntities = this.service.ReadAsync(0, 2).Result;
            var anonymousEntitiesAfter = readedEntities.Select(x => new { x.Id, x.SomeData });
            CollectionAssert.AreEquivalent(anonymousEntitiesBefore, anonymousEntitiesAfter);

            anonymousEntitiesBefore = apiEntities.Where(x => x.SomeData == "3")
                                                 .Select(x => new { x.Id, x.SomeData })
                                                 .Skip(1)
                                                 .Take(1);
            readedEntities = this.service.ReadAsync(x => x.SomeData == "3", 1, 1).Result;
            anonymousEntitiesAfter = readedEntities.Select(x => new { x.Id, x.SomeData });
            CollectionAssert.AreEquivalent(anonymousEntitiesBefore, anonymousEntitiesAfter);

            anonymousEntitiesBefore = apiEntities.Select(x => new { x.Id, x.SomeData })
                                                 .Skip(1)
                                                 .Take(2);
            readedEntities = this.service.ReadAsync(1, 2).Result;
            anonymousEntitiesAfter = readedEntities.Select(x => new { x.Id, x.SomeData });
            CollectionAssert.AreEquivalent(anonymousEntitiesBefore, anonymousEntitiesAfter);
        }

        [Test]
        public void CanRemoveEntities()
        {
            var apiEntity1 = new ApiSample
            {
                Id = Guid.NewGuid(),
                SomeData = "testData"
            };
            var apiEntity2 = new ApiSample
            {
                Id = Guid.NewGuid(),
                SomeData = "testData"
            };
            var apiEntity3 = new ApiSample
            {
                Id = Guid.NewGuid(),
                SomeData = "testData"
            };

            this.service.Write(apiEntity1);
            this.service.Remove(apiEntity1);
            var readedEntities = this.service.ReadAsync(x => !x.IsDeleted).Result;
            Assert.AreEqual(0, readedEntities.Count);

            this.service.Write(new[] { apiEntity2, apiEntity3 });
            this.service.Remove(new[] { apiEntity2, apiEntity3 });
            readedEntities = this.service.ReadAsync(x => !x.IsDeleted).Result;
            Assert.AreEqual(0, readedEntities.Count);
        }

        [Test]
        public void CanReadSyncedData()
        {
            ApiSample[] apiEntities;
            Guid[] deletedIds;
            var sync = this.service.ReadSyncedData(0, out apiEntities, out deletedIds);

            Assert.AreEqual(0, sync);

            var apiEntity1 = new ApiSample
            {
                Id = Guid.NewGuid()
            };
            this.service.Write(apiEntity1);

            var apiEntity2 = new ApiSample
            {
                Id = Guid.NewGuid()
            };
            this.service.Write(apiEntity2);

            sync = this.service.ReadSyncedData(sync, out apiEntities, out deletedIds);

            Assert.AreEqual(2, apiEntities.Length);
            Assert.AreEqual(2, sync);

            var previousSync = sync;
            sync = this.service.ReadSyncedData(sync, out apiEntities, out deletedIds);

            Assert.AreEqual(previousSync, sync);

            this.service.Remove(apiEntity2);
            sync = this.service.ReadSyncedData(sync, out apiEntities, out deletedIds);
            Assert.AreEqual(1, deletedIds.Length);
            Assert.AreEqual(3, sync);
        }

        [Test]
        public void CanReadSyncedDataWithFilter()
        {
            ApiSample[] apiEntities;
            Guid[] deletedIds;
            var apiEntity1 = new ApiSample
            {
                Id = Guid.NewGuid(),
                SomeData = "1"
            };
            this.service.Write(apiEntity1);

            var apiEntity2 = new ApiSample
            {
                Id = Guid.NewGuid(),
                SomeData = "2"
            };
            this.service.Write(apiEntity2);

            var sync = this.service.ReadSyncedData(0, out apiEntities, out deletedIds, x => x.SomeData == "2");

            Assert.AreEqual(1, apiEntities.Length);
            Assert.AreEqual(2, sync);
        }
    }
}
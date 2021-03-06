﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using Beetle.Server.Meta;
#if EF6
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Core.Metadata.Edm;
using EFQueryHandler = Beetle.Server.EntityFramework.EF6QueryHandler;
using EFState = System.Data.Entity.EntityState;
using EntityType = System.Data.Entity.Core.Metadata.Edm.EntityType;
#else
using System.Data.EntityClient;
using System.Data.Metadata.Edm;
using System.Data.Objects;
using EFQueryHandler = Beetle.Server.QueryableHandler;
using EFState = System.Data.EntityState;
using EntityType = System.Data.Metadata.Edm.EntityType;
#endif

namespace Beetle.Server.EntityFramework {

    /// <summary>
    /// Entity Framework ORM context handler.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    public class EFContextHandler<TContext> : DbContextHandler<TContext> {

        #region Fields, Properties

        private DbContext _dbContext;
        private IObjectContextAdapter _objectContextAdapter;
        private static readonly object _objectContextLocker = new object();
        private ObjectContext __objectContext;
        private ObjectContext _objectContext {
            get {
                if (__objectContext != null) return __objectContext;
                lock (_objectContextLocker) {
                    return __objectContext ?? (__objectContext = _objectContextAdapter.ObjectContext);
                }
            }
        }
        private static readonly object _itemCollectionLocker = new object();
        private ItemCollection __itemCollection;
        private ItemCollection _itemCollection {
            get {
                if (__itemCollection != null) return __itemCollection;
                lock (_itemCollectionLocker) {
                    return __itemCollection ?? (__itemCollection = _objectContext.MetadataWorkspace.GetItemCollection(DataSpace.CSpace));
                }
            }
        }
        private IEnumerable<EntityType> __entityTypes;
        private IEnumerable<EntityType> _entityTypes {
            get {
                if (__entityTypes != null) return __entityTypes;
                lock (_itemCollection) {
                    return __entityTypes ?? (__entityTypes = _itemCollection.OfType<EntityType>().ToList());
                }
            }
        }
        private IEnumerable<EntitySetBase> __entitySets;
        private IEnumerable<EntitySetBase> _entitySets {
            get {
                if (__entitySets != null) return __entitySets;
                lock (_itemCollection) {
                    return __entitySets ?? (__entitySets = _itemCollection.OfType<EntityContainer>().SelectMany(ec => ec.BaseEntitySets).ToList());
                }
            }
        }
        private static readonly object _objectOtemCollectionLocker = new object();
        private ObjectItemCollection __objectItemCollection;
        private ObjectItemCollection _objectItemCollection {
            get {
                if (__objectItemCollection != null) return __objectItemCollection;
                lock (_objectOtemCollectionLocker) {
                    if (typeof(ObjectContext).IsAssignableFrom(typeof(TContext)))
                        _objectContext.MetadataWorkspace.LoadFromAssembly(_objectContext.GetType().Assembly);
                    return __objectItemCollection ??
                        (__objectItemCollection = (ObjectItemCollection)_objectContext.MetadataWorkspace.GetItemCollection(DataSpace.OSpace));
                }
            }
        }
        private List<EntityType> __objectEntityTypes;
        private List<EntityType> _objectEntityTypes {
            get {
                if (__objectEntityTypes != null) return __objectEntityTypes;
                lock (_objectItemCollection) {
                    if (__objectEntityTypes == null) {
                        var objectEntityTypes = __objectItemCollection.GetItems<EntityType>();
                        __objectEntityTypes = objectEntityTypes != null ? objectEntityTypes.ToList() : new List<EntityType>();
                    }
                    return __objectEntityTypes;
                }
            }
        }

        private static readonly object _metadataLocker = new object();
        private static Metadata _metadata;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="EFContextHandler{TContext}"/> class.
        /// </summary>
        public EFContextHandler()
            : this(EFQueryHandler.Instance) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EFContextHandler{TContext}"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public EFContextHandler(TContext context)
            : this(context, EFQueryHandler.Instance) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EFContextHandler{TContext}"/> class.
        /// </summary>
        /// <param name="queryableHandler">The queryable handler.</param>
        public EFContextHandler(IQueryHandler<IQueryable> queryableHandler)
            : base(queryableHandler) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EFContextHandler{TContext}"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="queryableHandler">The queryable handler.</param>
        public EFContextHandler(TContext context, IQueryHandler<IQueryable> queryableHandler)
            : base(context, queryableHandler) {
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public override void Initialize() {
            base.Initialize();

            __objectContext = Context as ObjectContext;
            if (__objectContext == null) {
                _objectContextAdapter = Context as IObjectContextAdapter;
                _dbContext = Context as DbContext;
            }
            if (__objectContext == null && _objectContextAdapter == null)
                throw new InvalidOperationException("Type parameter must inherit from ObjectContext or implement IObjectContextAdapter.");

            ValidateOnSaveEnabled = _dbContext == null;
            if (__objectContext != null) {
                __objectContext.ContextOptions.LazyLoadingEnabled = false;
                __objectContext.ContextOptions.ProxyCreationEnabled = false;
            }
            else if (_dbContext != null) {
                _dbContext.Configuration.ProxyCreationEnabled = false;
                _dbContext.Configuration.LazyLoadingEnabled = false;
            }
        }

        /// <summary>
        /// Return metadata about data structure.
        /// </summary>
        /// <returns>
        /// Metadata object.
        /// </returns>
        public override Metadata Metadata() {
            if (_metadata != null) return _metadata;
            lock (_metadataLocker) {
                var a = typeof(TContext).Assembly;
                var c = _objectContext.Connection.ConnectionString;
                return _metadata ?? (_metadata = MetadataGenerator.Generate(_objectContext.MetadataWorkspace, _itemCollection, _objectItemCollection, a, c));
            }
        }

        /// <summary>
        /// Creates the type by name.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <returns></returns>
        public override object CreateType(string typeName) {
            if (_objectEntityTypes != null) {
                var oType = _objectEntityTypes.FirstOrDefault(x => x.Name == typeName);
                if (oType != null) {
                    var clrType = _objectItemCollection.GetClrType(oType);
                    return Activator.CreateInstance(clrType);
                }
            }

            return base.CreateType(typeName);
        }

        /// <summary>
        /// Creates the queryable.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <returns></returns>
        public override IQueryable<TEntity> CreateQueryable<TEntity>() {
            if (_dbContext != null)
                return _dbContext.Set<TEntity>();

            var setName = FindEntitySet(typeof(TEntity));
            IQueryable<TEntity> query = _objectContext.CreateQuery<TEntity>(string.Format("[{0}]", setName));
            return query.OfType<TEntity>();
        }

        /// <summary>
        /// Merges the entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="unmappedEntities">The unmapped entities.</param>
        /// <returns>Merges entities.</returns>
        public override IEnumerable<EntityBag> MergeEntities(IEnumerable<EntityBag> entities, out IEnumerable<EntityBag> unmappedEntities) {
            if (entities == null)
                throw new ArgumentNullException("entities");

            var unmappeds = new List<EntityBag>();
            var entityList = entities as IList<EntityBag> ?? entities.ToList();
            var mergeList = new Dictionary<ObjectStateEntry, EntityBag>();

            foreach (var entityBag in entityList) {
                var entity = entityBag.Entity;
                var entityType = _entityTypes.FirstOrDefault(et => et.Name == entity.GetType().Name);
                EntitySetBase set = null;
                if (entityType != null)
                    set = FindEntitySet(entityType);
                if (set == null) unmappeds.Add(entityBag);
                else {
                    var state = entityBag.EntityState;

                    // attach entity to entity set
                    _objectContext.AddObject(set.Name, entity);
                    var entry = _objectContext.ObjectStateManager.GetObjectStateEntry(entity);
                    entry.ChangeState(EFState.Unchanged);
                    mergeList.Add(entry, entityBag);

                    // set original values for modified entities
                    if (state == EntityState.Modified) {
                        var originalValues = entry.GetUpdatableOriginalValues();

                        foreach (var originalValue in entityBag.OriginalValues) {
                            var propertyPaths = originalValue.Key.Split('.');
                            if (!propertyPaths.Any()) continue;

                            var loopOriginalValues = originalValues;
                            StructuralType loopType = entityType;
                            EdmMember property = null;
                            string lastPropertyName;
                            if (propertyPaths.Length > 1) {
                                for (var i = 0; i < propertyPaths.Length - 1; i++) {
                                    var propertyName = propertyPaths[i];
                                    property = loopType.Members.FirstOrDefault(p => p.Name == propertyName);
                                    if (property == null) break;

                                    var ordinal = loopOriginalValues.GetOrdinal(propertyName);
                                    loopOriginalValues = loopOriginalValues.GetValue(ordinal) as OriginalValueRecord;
                                    loopType = property.TypeUsage.EdmType as StructuralType;
                                }
                                if (property == null) continue;

                                lastPropertyName = propertyPaths[propertyPaths.Length - 1];
                            }
                            else
                                lastPropertyName = originalValue.Key;

                            property = loopType.Members.FirstOrDefault(p => p.Name == lastPropertyName);
                            if (property == null) continue;

                            // if modified property is a ComplexType then set all properties of ComplexType to modified.
                            var complexType = property.TypeUsage.EdmType as ComplexType;
                            if (complexType != null)
                                PopulateComplexType(loopOriginalValues, property.Name, originalValue.Value, complexType);
                            else {
                                var ordinal = loopOriginalValues.GetOrdinal(property.Name);
                                loopOriginalValues.SetValue(ordinal, originalValue.Value);
                            }
                        }
                    }
                }
            }

            foreach (var merge in mergeList) {
                // and fix the entity state, and relations will also be fixed.
                if (merge.Value.EntityState == EntityState.Modified) {
                    if (merge.Key.State != EFState.Modified && merge.Value.ForceUpdate)
                        merge.Key.ChangeState(EFState.Modified);
                }
                else
                    merge.Key.ChangeState((EFState)merge.Value.EntityState);
            }

            unmappedEntities = unmappeds;
            return mergeList.Select(m => m.Value);
        }

        /// <summary>
        /// Populates complex type original values to OriginalValueRecord.
        /// </summary>
        /// <param name="originalValues">The original values.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="originalValue">The original value.</param>
        /// <param name="complexType">ComplexType.</param>
        private static void PopulateComplexType(OriginalValueRecord originalValues, string propertyName, object originalValue, ComplexType complexType) {
            var ordinal = originalValues.GetOrdinal(propertyName);
            originalValues = originalValues.GetValue(ordinal) as OriginalValueRecord;
            foreach (var cp in complexType.Properties) {
                var value = Helper.GetPropertyValue(originalValue, cp.Name);

                var loopComplexType = cp.TypeUsage.EdmType as ComplexType;
                if (loopComplexType != null)
                    PopulateComplexType(originalValues, cp.Name, value, loopComplexType);
                else {
                    var loopOrdinal = originalValues.GetOrdinal(cp.Name);
                    originalValues.SetValue(loopOrdinal, value);
                }
            }
        }

        /// <summary>
        /// Saves the changes.
        /// </summary>
        /// <param name="entityBags">The entity bags.</param>
        /// <param name="saveContext">The save context.</param>
        /// <returns>
        /// Save result.
        /// </returns>
        /// <exception cref="EntityValidationException"></exception>
        public override SaveResult SaveChanges(IEnumerable<EntityBag> entityBags, SaveContext saveContext) {
            IEnumerable<EntityBag> unmappeds;
            var merges = MergeEntities(entityBags, out unmappeds);
            var mergeList = merges == null
                ? new List<EntityBag>()
                : merges as List<EntityBag> ?? merges.ToList();

            var saveList = mergeList;
            var handledUnmappeds = HandleUnmappeds(unmappeds);
            var handledUnmappedList = handledUnmappeds == null ? null : handledUnmappeds as IList<EntityBag> ?? handledUnmappeds.ToList();
            if (handledUnmappedList != null && handledUnmappedList.Any()) {
                IEnumerable<EntityBag> discarded;
                MergeEntities(handledUnmappedList, out discarded);
                saveList = saveList.Concat(handledUnmappedList).ToList();
            }
            if (!saveList.Any()) return SaveResult.Empty;

            OnBeforeSaveChanges(new BeforeSaveEventArgs(saveList, saveContext));
            // do data annotation validations
            if (ValidateOnSaveEnabled) {
                var toValidate = saveList.Where(eb => eb.EntityState == EntityState.Added || eb.EntityState == EntityState.Modified).Select(eb => eb.Entity);
                var validationResults = Helper.ValidateEntities(toValidate);
                if (validationResults.Any())
                    throw new EntityValidationException(validationResults);
            }
            var affectedCount = _dbContext != null ? _dbContext.SaveChanges() : _objectContext.SaveChanges();

            var generatedValues = GetGeneratedValues(mergeList);
            if (handledUnmappedList != null && handledUnmappedList.Any()) {
                var generatedValueList = generatedValues == null
                    ? new List<GeneratedValue>()
                    : generatedValues as List<GeneratedValue> ?? generatedValues.ToList();
                generatedValueList.AddRange(GetHandledUnmappedGeneratedValues(handledUnmappedList));
                generatedValues = generatedValueList;
            }

            var saveResult = new SaveResult(affectedCount, generatedValues, saveContext.GeneratedEntities, saveContext.UserData);
            OnAfterSaveChanges(new AfterSaveEventArgs(saveList, saveResult));

            return saveResult;
        }

        /// <summary>
        /// Finds the entity set.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <returns></returns>
        private EntitySetBase FindEntitySet(EdmType entityType) {
            while (entityType != null) {
                var set = _entitySets.FirstOrDefault(es => es.ElementType == entityType);
                if (set != null) return set;
                entityType = entityType.BaseType;
            }
            return null;
        }

        /// <summary>
        /// Finds the entity set.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <returns></returns>
        private EntitySetBase FindEntitySet(Type entityType) {
            while (entityType != null) {
                var set = _entitySets.FirstOrDefault(es => es.ElementType.Name == entityType.Name);
                if (set != null) return set;
                entityType = entityType.BaseType;
            }
            return null;
        }

        /// <summary>
        /// Gets the connection.
        /// </summary>
        /// <value>
        /// The connection.
        /// </value>
        public override IDbConnection Connection {
            get {
                return ((EntityConnection)_objectContext.Connection).StoreConnection;
            }
        }

        /// <summary>
        /// Gets the model namespace.
        /// </summary>
        /// <value>
        /// The model namespace.
        /// </value>
        public override string ModelNamespace {
            get { return typeof(TContext).Namespace; }
        }

        /// <summary>
        /// Gets the model assembly.
        /// </summary>
        /// <value>
        /// The model assembly.
        /// </value>
        public override string ModelAssembly {
            get { return typeof(TContext).Assembly.GetName().Name; }
        }
    }
}
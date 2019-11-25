---
layout: post
title: "Clearing Entity Fields in Early Bound Plugin Code"
published: "2017-02-22"
---

If you try to `NULL` the value of an Entity field and save that change with the CRM OrganizationServiceContext, you need to be careful how you set that NULL value.  If the field to NULL is not in the `myEntity.Attributes` collection, then it will not be updated when the service call updates the record in CRM.

We can demonstrate this by initializing the early bound Account entity in a few different ways and inspecting the Attribute collection.  The field to clear in this example will be `ParentAccountId`.

**First**, we will use the constructor then NULL with dot notation.

**Second**, we use the object initializer syntax and null with that.

**Third**, we initialize only the ID field and use dot notation to set the field NULL.

These attempts will not put ParentAcountId into the Attributes collection.  Two ways that will work are setting the field to NULL with the late bound class, and initializing or setting ParentAccountId with a dummy non-NULL value in the early bound class then setting the field to NULL.

This test class will demonstrate each of these approaches.

```c#
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using MyXRM.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyXRM.Tests
{
    [TestClass]
    public class AttributeCollectionTest
    {
        [TestMethod]
        public void TestWhatAddsAttributeToCollection()
        {
            // set up the account to reference
            var myAccountInCRM = new Account();
            myAccountInCRM.Id = Guid.NewGuid();
            myAccountInCRM.Name = "Test Name";
            myAccountInCRM.ParentAccountId = new EntityReference(Account.EntityLogicalName, Guid.NewGuid());
            Assert.IsTrue(myAccountInCRM.Attributes.ContainsKey("parentaccountid"));

            // list of pass/fails for each attempt
            var results = new List<bool>();

            /*
             * Below are different ways of initializing the early bound Account entity
             * with the field we want to clear.
             */

            // 
            var withConstructor = new Account();
            withConstructor.Id = myAccountInCRM.Id;
            withConstructor.ParentAccountId = null;
            results.Add(withConstructor.Attributes.ContainsKey("parentaccountid"));

            var withInitializer = new Account
            {
                Id = myAccountInCRM.Id,
                ParentAccountId = null
            };
            results.Add(withInitializer.Attributes.ContainsKey("parentaccountid"));

            var withInitializedId_ThenUpdate = new Account
            {
                Id = myAccountInCRM.Id
            };
            withInitializedId_ThenUpdate.ParentAccountId = null;
            results.Add(withInitializedId_ThenUpdate.Attributes.ContainsKey("parentaccountid"));

            var lateBound = new Entity
            {
                Id = myAccountInCRM.Id
            };
            lateBound["parentaccountid"] = null;
            results.Add(lateBound.Attributes.ContainsKey("parentaccountid"));

            var withInitializer_ActuallyClearsField = new Account
            {
                Id = myAccountInCRM.Id,
                ParentAccountId = new EntityReference()
            };
            withInitializer_ActuallyClearsField.ParentAccountId = null;
            results.Add(withInitializer_ActuallyClearsField.Attributes.ContainsKey("parentaccountid"));

            Console.WriteLine("Test Results: {0}",
                String.Join(",", results));
            Assert.IsTrue(results.Count(x => x == true) == 2,
                "Only two of these cases should have passed.");
            Assert.IsTrue(results[results.Count - 2],
                "The late bound example should have been true");
            Assert.IsTrue(results.Last(),
                "The last result should have been true in this demo.");
        }
    }
}
```

In this case, **using the late bound entity is more straightforward** than using the early bound entity.  With late bound you will not get intellisense so make sure you have the correct spelling and casing for your field.  You can find the correct string to use in your early bound entities file by hitting F12 on the early bound field and inspecting the method decorator.  For our field, we use the string in here: `[Microsoft.Xrm.Sdk.AttributeLogicalNameAttribute("parentaccountid")]`.

If you use the <a href="https://github.com/daryllabar/DLaB.Xrm.XrmToolBoxTools/releases">Early Bound Generator tool</a> from the <a href="http://www.xrmtoolbox.com/">XRM Toolbox</a>, one particularly useful thing it does is enumerates each attribute name as a struct of strings.  That provides intellisense and the correctly cased string name of the field.

Initializing an early bound entity field with NULL looks like code that should work, but chances are you only notice the problem when the update does not clear that field in CRM.  You could just as easily do `earlyBoundAccount["parentaccountid"] = null;`, but why would that be your first choice when you have early bound classes?

You might consider a wrapper class to handle this NULL setting logic for you, or probably simpler still an extension method `SetToNull(myAccount, "nameOfFieldToClear")` so you can use this for all entities.  Remember to use the `Fields` struct if you use the Early Bound Generator to create your early bound classes - `SetToNull(myAccount, Account.Fields.NameOfFieldToClear)`.

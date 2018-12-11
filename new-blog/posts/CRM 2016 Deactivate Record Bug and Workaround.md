# CRM 2016 Deactivate Record Bug and Workaround

---
layout: post
title: CRM 2016 Deactivate Record Bug and Workaround
published: "2016-08-22"
tags: ["CRM 2016"]
---

### Summary

**Problem:**

If an entity record is missing required fields, you get an error when trying to deactivate the record from the form.

**Solution Summary:**

I assume you know how to use RibbonWorkbench to edit entity ribbons so I gloss over the setup specifics.  Review the [Getting Started Guide at the author's website](https://ribbonworkbench.uservoice.com/knowledgebase/articles/71374-1-getting-started-with-the-ribbon-workbench) and the [CRM 2016 RibbonWorkbench beta announcement post](http://develop1.net/public/post/Ribbon-Workbench-2016-Beta.aspx) for more information about Ribbon Workbench.

1. Open a solution containing the entities you want to fix in Ribbon Workbench.
2. Add a Custom Javascript Action **above** the existing Custom Javascript Action.  Our new action must execute first.
3. Have the action call a function that does the following:
    1. remove the required level from all form fields then return.  This must be synchronous code 		 because the next Action will execute immediately after the first action returns.  It should        remember which fields were required if you want to restore them after `statecode` changes.
    2. *(optional)* add an OnChange event to the `statecode` attribute (make sure this is on the form) to restore the required level to the correct attributes.
4. Publish the solution from Ribbon Workbench.

---

### The Workaround

In CRM 2016 ([and similarly for others in 2013+](https://community.dynamics.com/crm/f/117/t/117841)), we ran into an odd error around deactivating Accounts and Contacts from their forms.  This likely can happen on any record having a Deactivate button.  If a Contact record is missing a required field denoted by a red \*, then clicking the Deactivate button and completing the popup window by clicking OK, you get a not so helpful error message:

![Popup saying An Error has occurred. Please return to the home page and try again.](http://i.imgur.com/RZGPVLx.png)
(The obscured window is the "Confirm Deactivation" CRM lightbox.)

If you fill in the required fields and try again (with or without saving the form), then the Deactivate button click works.  Deactivating the record from a homepage grid or subgrid works regardless of the required fields.  The grid approach does not need required fields to be filled.  Why does the form need it?  Since the required fields were the apparent blockers, I thought the button was changing the statecode and statuscode fields, saving the form, and failing because you can't save the form when required fields are empty.  We have to see how the Deactivate button works, and I used [Ribbon Workbench for CRM 2016 (beta)](https://community.dynamics.com/crm/b/develop1/archive/2016/04/25/ribbon-workbench-2016-beta) to see the function name I need to find.

![Deactivate Account form button in Ribbon Workbench](http://i.imgur.com/GbvTDwn.png)

The bottom right **Custom Javascript Action** is what an uncustomized Deactivate Button command does when clicked.  Ignore the action above it for now - it is the workaround I will describe later.

The RibbonWorkbench showed me the library and function the Deactivate button calls - `CommandBarActions.js` and `Mscrm.CommandBarActions.changeState`.  If I am on the Account form, the button calls `Mscrm.CommandBarActions.changeState("deactivate", "{my-account-guid}", "account")`.  I copied into a gist the code that I followed while trying to mentally trace what happens when Deactivate is clicked in our scenario.  It is not the full CommandBarActions.js file.  I don't find a definitive answer, but if you want to read the optional ramblings, follow the comments from top to bottom in this gist.  It is suffice to know that empty required fields are the root of the problem that we can fix.

I think this is a bug in CRM 2016 forms, but we can work around it in a supported way.  I wonder why the form does not do a [specialized UpdateRequest](https://msdn.microsoft.com/en-us/library/dn932124.aspx) (fancy name for "just update the statecode and statuscode in the UpdateRequest") through REST or WebApi?  It might be on a backlog somewhere.
<script src="https://gist.github.com/gfritz/99f3a44e2723da5a4980fc739f4c5d8e.js"></script>

Check the top right Custom Javascript Action again.  Notice the Custom Javascript Action called `deactivateFromFormWorkaround` taking `PrimaryEntityTypeName` as a parameter.  This will temporarily remove the required level from required fields so deactivating from the form will complete.
![Custom Javascript Action Workaround with Ribbon Workbench](http://i.imgur.com/GbvTDwn.png)

<script src="https://gist.github.com/gfritz/fd6a70dfb5258f666b85a217bc289efe.js"></script>

This code could have instead done a Metadata query to retrieve which fields are required for this form.  The SDK javascript libraries do asynchronous calls, and you can modify the functions to add a parameter to make them synchronous calls if you want.  I think the presented approach is simpler and definitely less code.  You do not have to restore the required levels as it is just a cleanup step.

One problem with this approach is if the user cancels the Deactivate confirmation, then the formerly required fields will still be not required.
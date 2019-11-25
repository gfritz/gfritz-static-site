---
layout: post
title: "CRM 2016 Deactivate Record on Form With Emtpy Required Fields Bug and Workaround"
published: "2016-08-22"
---

### Summary

#### Problem:

If an entity record is missing required fields, you get an error when trying to deactivate the record from the form.

#### Solution Summary

I assume you know how to use RibbonWorkbench to edit entity ribbons so I gloss over the setup specifics.  Review the [Getting Started Guide at the author's website]("https://ribbonworkbench.uservoice.com/knowledgebase/articles/71374-1-getting-started-with-the-ribbon-workbench") and the [CRM 2016 RibbonWorkbench beta announcement post]("http://develop1.net/public/post/Ribbon-Workbench-2016-Beta.aspx") for more information about Ribbon Workbench.

1. Open a solution containing the entities you want to fix in Ribbon Workbench.
2. Add a Custom Javascript Action **above** the existing Custom Javascript Action.  Our new action must execute first.
3. Have the action call a function that does the following:
    1. Remove the required level from all form fields then return. This must be synchronous code because the next Action will execute immediately after the first action returns.  It should remember which fields were required if you want to restore them after `statecode` changes.
    2. *(optional)* add an OnChange event to the `statecode` attribute (make sure this is on the form) to restore the required level to the correct attributes.
4. Publish the solution from Ribbon Workbench.

<hr />

### Solution/Workaround, Longer Form

In CRM 2016, [and similarly for others in 2013+](https://community.dynamics.com/crm/f/117/t/117841), we ran into an odd error around deactivating Accounts and Contacts from their forms.  This likely can happen on any record having a Deactivate button.  If a Contact record is missing a required field denoted by a red asterisk (*), then clicking the Deactivate button and completing the popup window by clicking OK, you get a not so helpful error message:

![Popup saying An Error has occurred. Please return to the home page and try again.](https://i.imgur.com/RZGPVLx.png "A really vague CRM error message")

The obscured window is the "Confirm Deactivation" CRM lightbox.

If you fill in the required fields and try again (with or without saving the form), then the Deactivate button click works.  Deactivating the record from a homepage grid or subgrid works regardless of the required fields.  The grid approach does not need required fields to be filled.  Why does the form need it?  Since the required fields were the apparent blockers, I thought the button was changing the statecode and statuscode fields, saving the form, and failing because you can't save the form when required fields are empty.  We have to see how the Deactivate button works, and I used [Ribbon Workbench for CRM 2016 (beta)]("https://community.dynamics.com/crm/b/develop1/archive/2016/04/25/ribbon-workbench-2016-beta") to see the function name I need to find.

![Deactivate Account form button in Ribbon Workbench](https://i.imgur.com/GbvTDwn.png)

The bottom right **Custom Javascript Action** is what an uncustomized Deactivate Button command does when clicked. Ignore the action above it for now - it is the workaround I will describe later.

The RibbonWorkbench showed me the library and function the Deactivate button calls - `CommandBarActions.js` and `Mscrm.CommandBarActions.changeState`. If I am on the Account form, the button calls `Mscrm.CommandBarActions.changeState("deactivate", "{my-account-guid}", "account")`. At the end of this post is the code that I followed while trying to mentally trace what happens when Deactivate is clicked in our scenario. It is not the full `CommandBarActions.js` file. I do not find a definitive answer, but if you want to read the optional ramblings, follow the comments from top to bottom in this code block. It is suffice to know that empty required fields are the root of the problem that we can fix.

I think this is a bug in CRM 2016 forms, but we can work around it in a supported way.  I wonder why the form does not do a [specialized UpdateRequest]("https://msdn.microsoft.com/en-us/library/dn932124.aspx") (fancy name for "just update the statecode and statuscode in the UpdateRequest") through REST or WebApi?  It might be on a backlog somewhere.

Check the top right Custom Javascript Action again.  Notice the Custom Javascript Action called `deactivateFromFormWorkaround` taking `PrimaryEntityTypeName` as a parameter.  This will temporarily remove the required level from required fields so deactivating from the form will complete.

![Custom Javascript Action Workaround with Ribbon Workbench](https://i.imgur.com/GbvTDwn.png)

```js
// Remove Required Level from Fields so Deactivate Works on CRM 2016 form, then restore after the statecode changes 

// XrmCommon.removeOnChange and XrmCommon.addOnChange call the same Xrm.Page methods but check if the field exists on the form first.

// CommandProperties is always passed as the first parameter in Ribbon Button Actions
function deactivateFromFormWorkaround(CommandProperties, PrimaryEntityTypeName) {
    var restoreRequiredFields = function (context) {
        XrmCommon.undoRemoveRequiredLevel();
        XrmCommon.removeOnChange("statecode", restoreRequiredFields);
    };
    var permittedEntities = ["account", "contact"];
    if (permittedEntities.indexOf(PrimaryEntityTypeName) === -1) {
        console.error(PrimaryEntityTypeName + " is not supported for this Deactivate button workaround.");
    }
    XrmCommon.removeRequiredLevel();
    XrmCommon.addOnChange("statecode", restoreRequiredFields);
}

// XrmCommon is normally in another js file, so I'm adding just the relevant code to this gist.
var XrmCommon = XrmCommon || {};

XrmCommon._requiredFields = [];
XrmCommon.removeRequiredLevel = function () {
    /// <summary>Removes required level from all required fields</summary>
    Xrm.Page.getAttribute(function (attribute, index) {
        if (attribute.getRequiredLevel() == "required") {
            attribute.setRequiredLevel(XrmCommon.CONSTANTS.FORM_REQUIRED_LEVEL_NONE);
            XrmCommon._requiredFields.push(attribute.getName());
        }
    });
}
XrmCommon.undoRemoveRequiredLevel = function () {
    if (XrmCommon._requiredFields.length == 0) {
        _xrmCommonConsoleWarning("Nonsensical call to XrmCommon.undoRemoveRequiredLevel without calling XrmCommon.removeRequiredLevel first");
    }
    else {
        var affectedFieldNames = XrmCommon._requiredFields;
        for (var name in affectedFieldNames) {
            XrmCommon.setFieldRequirementLevel(affectedFieldNames[name], XrmCommon.CONSTANTS.FORM_REQUIRED_LEVEL_REQUIRED);
        }
        XrmCommon._requiredFields.length = 0;
    }
}
XrmCommon.CONSTANTS = {
    FORM_REQUIRED_LEVEL_NONE: "none",
    FORM_REQUIRED_LEVEL_RECOMMENDED: "recommended",
    FORM_REQUIRED_LEVEL_REQUIRED: "required"
};
```

This code could have instead done a Metadata query to retrieve which fields are required for this form. The SDK javascript libraries do asynchronous calls, and you can modify the functions to add a parameter to make them synchronous calls if you want. I think the presented approach is simpler and definitely less code. You do not have to restore the required levels as it is just a cleanup step.

**One problem with this approach** is if the user cancels the Deactivate confirmation, then the formerly required fields will still be not required.

That's it! Hopefully updates to CRM fix this weird behavior.

### CRM Javascript and Ramblings

This is the code block referenced above.

```js
// SUMMARY if you don't want to read the whole thing
// If this branch is followed and does the return "if (!Xrm.Page.data.getIsValid()) return;", 
//      then I think the "please try again" popup happens because "Xrm.Page.data.save($v_5).then($v_0, $v_1)" has a problem.
// Otherwise, I think the "please try again" popup happens because getIsValid makes this command return earlier than expected
// I find the specific message defined as the global variable LOCID_IPADWINCLOSED,
// but I don't find how calling Mscrm.CommandBarActions.changeState() directly from the ribbon in this scenario throws that message.

// clicking on Account form calls: Mscrm.CommandBarActions.changeState("deactivate", "{my-account-guid}", "Account")
Mscrm.CommandBarActions.changeState = function(action, entityId, entityName) {
    Mscrm.CommandBarActions.handleStateChangeAction(action, entityId, entityName)
};
Mscrm.CommandBarActions.handleStateChangeAction = function(action, entityId, entityName) {
    var $v_0 = null;
    if (Mscrm.CommandBarActions.isWebClient() || Xrm.Page.context.client.getClient() === "Outlook") {
        $v_0 = new Xrm.DialogOptions;
        $v_0.height = 230;
        $v_0.width = 600
    }
    // entityName = "account" makes this if guard false,
    if (Mscrm.InternalUtilities.DialogUtility.isMDDConverted(action, entityName)) {
        var $v_1 = new Microsoft.Crm.Client.Core.Storage.Common.ObjectModel.EntityReference(entityName, new Microsoft.Crm.Client.Core.Framework.Guid(entityId)),
            $v_2 = [$v_1],
            $v_3 = {};
        $v_3["records"] = Mscrm.InternalUtilities.DialogUtility.serializeSdkEntityReferences($v_2);
        $v_3["action"] = action;
        $v_3["lastButtonClicked"] = "";
        $v_3["state_id"] = -1;
        $v_3["status_id"] = -1;
        Xrm.Dialog.openDialog("SetStateDialog", $v_0, $v_3, Mscrm.CommandBarActions.closeSetStateDialogCallback, null)
    } else {
        $v_0.height = 250;
        $v_0.width = 420;
        var $v_4 = Xrm.Internal.getEntityCode(entityName),
            $v_5 = Mscrm.GridCommandActions.$L(action, $v_4, 1);
        $v_5.get_query()["iObjType"] = $v_4;
        $v_5.get_query()["iTotal"] = "1";
        $v_5.get_query()["sIds"] = entityId;
        $v_5.get_query()["confirmMode"] = "1";
        var $v_6 = [action, entityId, entityName],
            $v_7 = Mscrm.CommandBarActions.createCallbackFunctionFactory(Mscrm.CommandBarActions.performActionAfterChangeStateWeb, $v_6);
        // $v_6 is the args array to performActionAfterChangeStateWeb, so now check what that function does
        // when $v_6 = ["deactivate", "{my-account-guid}", "account"]
        Xrm.Internal.openDialog($v_5.toString(), $v_0, [entityId], null, $v_7)
    }
};
Mscrm.InternalUtilities.DialogUtility.isMDDConverted = function(action, entityName) {
    switch (action) {
        case "activate":
            switch (entityName) {
                case "audit":
                case "campaignresponse":
                case "channelaccessprofilerule":
                case "contract":
                case "service":
                case "sla":
                case "systemuser":
                case "workflow":
                    return false
            }
            break;
        case "deactivatecampactivity":
            return false;
        case "deactivate":
            switch (entityName) {
                case "audit":
                case "campaignresponse":
                case "channelaccessprofilerule":
                case "contract":
                case "service":
                case "sla":
                case "systemuser":
                case "workflow":
                    return false
            }
            break;
        case "delete":
            switch (entityName) {
                case "audit":
                case "service":
                case "workflow":
                case "hierarchyrule":
                    return false
            }
            break;
        case "converttoopportunity":
            switch (entityName) {
                case "serviceappointment":
                    return false
            }
            break;
        case "converttocase":
            switch (entityName) {
                case "serviceappointment":
                    return false
            }
            break;
        case "assign":
            switch (entityName) {
                case "connection":
                case "duplicaterule":
                case "emailserverprofile":
                case "goal":
                case "goalrollupquery":
                case "importmap":
                case "mailbox":
                case "mailmergetemplate":
                case "postfollow":
                case "queue":
                case "report":
                case "serviceappointment":
                case "sharepointdocumentlocation":
                case "sharepointsite":
                case "workflow":
                    return false
            }
            break
    }
    return true
};
Mscrm.CommandBarActions.createCallbackFunctionFactory = function(func, parameters) {
    return function(retValue) {
        parameters.unshift(retValue);
        return func.apply(null, parameters)
    }
};
Mscrm.CommandBarActions.performActionAfterChangeStateWeb = function(returnInfo, action, entityId, entityName) {
    var $v_0 = -1,
        $v_1 = 0;
    if (!Mscrm.InternalUtilities.JSTypes.isNull(returnInfo) && returnInfo) {
        var $v_2 = returnInfo;
        // $1U is a parseInt wrapper, so I'm not including it
        $v_0 = Mscrm.CommandBarActions.$1U($v_2["iStatusCode"]);
        $v_1 = Mscrm.CommandBarActions.$1U($v_2["iStateCode"]);
        // performActionAfterStateChange("deactivate", "{my-account-guid}", "account", newStateCodeFromDeactivateDialog, newStatusCodeFromDeactivateDialog, probablyReturnObject)
        Mscrm.CommandBarActions.performActionAfterStateChange(action, entityId, entityName, $v_1, $v_0, $v_2)
    }
};
Mscrm.CommandBarActions.performActionAfterStateChange = function(action, entityId, entityName, stateCode, statusCode, result) {
    var $v_0 = 0;
    switch (entityName) {
        // 
        case "account":
        case "contact":
        case "pricelevel":
        case "recommendationmodel":
        case "systemuser":
        case "topicmodel":
        case "knowledgesearchmodel":
            if (action === "activate") {
                stateCode = 0;
                Xrm.Page.context.saveMode = 6
            } else if (action === "deactivate") {
                stateCode = 1;
                // this is our entityName and action
                // but I don't know what saveMode = 5 does when required fields are empty
                // doesn't seem to do anything different when run in the console... moving down
                Xrm.Page.context.saveMode = 5
            }
            break;
        case "entitlement":
            if (action === "activate") stateCode = 1;
            else if (action === "deactivate") stateCode = 0;
            break;
        case "campaignactivity":
            if (action === "deactivatecampactivity") {
                $v_0 = 5;
                var $v_1 = new Mscrm.CampaignActivityStateHandler;
                $v_1.setDates(result["iStartDate"], result["iEndDate"]);
                $v_1.updateState()
            }
            break
    }
    if (action === "activate") $v_0 = 6;
    else if (action === "deactivate") $v_0 = 5;
    Xrm.Page.context.saveMode = $v_0;
    // setState calls $14 so it's a non-trivial enough wrapper to include here
    Mscrm.CommandBarActions.setState(entityId, entityName, stateCode, statusCode)
};
Mscrm.CommandBarActions.setState = function(entityId, entityName, stateCode, statusCode, closeWindow, entityToOpen, entityIdToOpen) {
    if (Mscrm.InternalUtilities.JSTypes.isNull(Xrm.Page.data.entity.getId())) return;
    // getIsValid is not documented, so I can't assume it checks required fields are filled, but I _think_ it does...
    // but this seems like a controlled return and not something that would make the popup "an error has occurrend please go the the homepage and try again"
    // I can't find the source of getIsValid() so I assume it returns true if required fields are empty
    if (!Xrm.Page.data.getIsValid()) return;
    // I think this is CRM trying to match your chosen statusCode to a stateCode
    // I assume the Confirm Deactivation lightbox picks only the StatusCode
    // either way, $14 still gets called so I don't think I need to include this setState function to read through
    // now look at $14
    if (typeof statusCode === "undefined") statusCode = -1;
    else if (stateCode === -1) {
        Xrm.Internal.getStateCodeFromStatusOption(entityName, statusCode).then(function($p1_0) {
            stateCode = $p1_0;
            Mscrm.CommandBarActions.$14(entityId, entityName, stateCode, statusCode, closeWindow, entityToOpen, entityIdToOpen)
        }, function() {
            Mscrm.CommandBarActions.$14(entityId, entityName, stateCode, statusCode, closeWindow, entityToOpen, entityIdToOpen)
        });
        return
    }
    Mscrm.CommandBarActions.$14(entityId, entityName, stateCode, statusCode, closeWindow, entityToOpen, entityIdToOpen)
};
// I think the dive finally ends here
// $v_0 seems to be the actual deactivate via Xrm.Internal.messages.setState()
Mscrm.CommandBarActions.$14 = function($p0, $p1, $p2, $p3, $p4, $p5, $p6) {
    var $v_0 = function($p1_0) {
            if (!$p0 || !$p0.length) $p0 = Xrm.Page.data.entity.getId();
            // if I'm on the web on a form, then I think this if guard is false
            // so we go to the else branch!
            if (Xrm.Utility.isMocaOffline()) {
                var $v_2 = new Microsoft.Crm.Client.Core.Storage.Common.ObjectModel.EntityReference($p1, new Microsoft.Crm.Client.Core.Framework.Guid($p0)),
                    $v_3 = new Microsoft.Crm.Client.Core.Storage.DataApi.Requests.SetStateRequest($v_2, $p2, $p3, true),
                    $v_4 = function() {
                        Mscrm.CommandBarActions.$1q($p0, $p1, $p4, $p5, $p6)
                    };
                Xrm.Utility.executeNonCudCommand("SetState", $p1, $v_3, $v_4, Mscrm.InternalUtilities.ClientApiUtility.actionFailedCallback)
            // looks like Xrm.Internal.messages.setState is a promise function
            // I assume setState works fine, but $1q tries to figure out what to do with the UI after the promise completes successfully
            // ALTHOUGH, $v_0 does not even get called until the form saves successfully... so lets go to Xrm.Page.data.save($v_5)
            } else Xrm.Internal.messages.setState($p1, $p0, $p2, $p3).then(function($p2_0) {
                Mscrm.CommandBarActions.$1q($p0, $p1, $p4, $p5, $p6)
            }, function($p2_0) {
                Mscrm.CommandBarActions.$O = false;
                Mscrm.InternalUtilities.ClientApiUtility.actionFailedCallback($p2_0)
            })
        },
        $v_1 = function($p1_0) {
            Mscrm.CommandBarActions.$O = false
        };
    if (!Mscrm.CommandBarActions.$O) {
        Mscrm.CommandBarActions.$O = true;
        var $v_5 = new Xrm.SaveOptions;
        $v_5.useSchedulingEngine = false;
        // I don't see why but maybe the save throws an error?  Otherwise, it might actually be .getIsValid returning early
        // that makes the message throw.
        Xrm.Page.data.save($v_5).then($v_0, $v_1)
    }
};
```
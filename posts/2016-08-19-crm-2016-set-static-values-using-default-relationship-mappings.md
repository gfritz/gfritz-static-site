---
layout: post
title: CRM 2016 Set Static Values Using Default Relationship Mappings
published: "2016-08-19"
tags: ["CRM 2016"]
---

**Requirement:** Replace freetext Assistant fields on the Contact form with a referential 1:N Contact relationship lookup field.

**Summary:**

1. Create a new Contact lookup field, refrential relationship type.
2. Notice that the default set of field mappings will default the Assistant field to the Contact you are creating the Assistant from.
3. Register and fire an OnChange event in the Quick Create form OnLoad event to check the Assistant GUID against the CreateFromId from QueryStringParameters.
4. If the GUIDs match, then you know this form is being used to create an Assistant.
5. Add JavaScript to the Assistant OnChange that runs when the GUIDs match to do other Assistant-only defaulting that the Relationship Mappings cannot do (or if non-mapped fields should not be changed by the user, then register a Pre-Create of Contact event plugin to set them instead of this JavaScript).

When creating a new lookup field, you have to create a new relationship.  CRM will generate a set of default mappings for new relationships.  We wanted to use a lookup to another Contact record instead of using the freetext fields "Assistant", "Assistant Email", and "Assistant Phone" on a Contact.

![Assistant Freetext Fields to replace with a Contact Lookup](http://i.imgur.com/o5f21cn.png)

In our example, we have created a 1:N relationship from Contact to Contact for a field called Assistant.  Any fields in the mappings list will populate onto the Create form for you.  If you want to use this defaulting in plugin code, you have to use the [InitializeFromRequest](https://msdn.microsoft.com/en-us/library/microsoft.crm.sdk.messages.initializefromrequest.aspx) or [WebApi InitializeFrom function](https://msdn.microsoft.com/en-us/library/mt683533.aspx).  One of the automatic field mappings defaults the Assistant field of the new record to the Contact from which it is being created.  This is a bad default because it is a circular reference, and CRM complains about it when you try to save the form.  If Assistant was not on the create form, this probably would not happen.  Our requirement specifically wants the field on the form though.

![ContactId to Assistant Mapping You Cannot Change](http://i.imgur.com/cpJpVNj.png)

We enabled the Contact entity for Quick Create forms, and the Assistant field is on the Quick Create form.  CRM does not let you delete or modify that relationship mapping so we have to do some client-side validation.  Add an OnChange event to the Assistant field and fire it in the OnLoad event of the Quick Create form.  The relationship populates the fields before OnLoad fires.  The OnChange event should clear the Assistant field if the GUID of the Assistant lookup matches the GUID of the source Contact record.  In a Quick Create form launched from a lookup field, you can get the source GUID from `Xrm.Page.context.getQueryStringParameters()._CreateFromId`.

If I am creating an Assistant for my Contact named "test, testington", then this is what the QueryStringParameters return object looks like.  Hit F12 when the Quick Create form is open then type that code - frames[0] or frames[1] might be necessary if the Xrm.Page object is sort of empty.
![GetQueryStringParameters example](http://i.imgur.com/JabCMpI.png)

<script src="https://gist.github.com/gfritz/0a1e6d196b93c6207ffcc81f9ce77833.js"></script>

The `XrmCommon` stuff is just my wrapper around the `Xrm.Page` object.  In this code block, the function names match Xrm.Page functions, and the only magic they do is check that a field exists on the form before calling the base `Xrm.Page` function.

Now you can do code specific to creating a new Assistant contact.  We know that if the Assistant lookup GUID matches the `_CreateFromId`, then this Quick Create form was launched from the Assistant lookup field on a Contact form.  This Quick Create form is going to create a new Assistant Contact!  In our case, the only additional default we wanted was to set the Relationship Type to a custom OptionSet value labeled "Assistant".  Relationship mappings alone cannot do that.  Since we only wanted one field defaulted, I just added one more line into the Assistant OnChange function that I already had.

If there were many more fields to set, more complicated logic to run, or other records to create or link while creating an Assistant, I would do the work in a plugin registered on the Pre-Operation Create event of Contacts.  If lots of fields were going to get defaulted that the user probably should not change, then good form design would be to not put them on the form at all, and use plugin code to set the defaults during the Pre-Create event.  Do it in the Pre-Create event to create the record with those values already set.  If you do it in the Post-Create event, it will still work, but you cause an Update Contact event which is another transaction and audit history entry.

I think if we did not put the Assistant field on the Quick Create form, we probably would not get the circular reference error in the first place, and we could have used a plugin to default the Relationship Type to Assistant during the Pre-Create stage.

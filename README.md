# D365-Dynamic-SQL-Server-Virtual-Entity-Provider
An SQL Server data provider for D365 supporting CRUD operations

## What does it do?
This is a connector intended for use with Azure SQL Server (though technically anything that takes an SQL Server connection string should probably work!)

**Note** - if you want to use Client ID/Secret credentials, use the azuread branch (it was working, but since I updated the code in this repo I haven't been able to retest it)

The connector uses the d365 attribute type and external names of the attribute to map your d365 columns to your SQL server columns. At present it supports most d365 record types, CRUD operations, paging and retrieves linked entity data in views.

The connector came about as I couldn't find any good ready to use SQL Server connectors for d365. Whilst the new Virtual entity connectors preview offering from MS looks promising I had a few issues with it. So, this project was born and is intended to be a light weight but functional alternative to this preview of the virtual entity connectors, creating OData services, or creating hard coded entity integrations for foreign entities.

## Usage
The project provides an unmanaged solution you can install that contains the plugin, data source entity, the datasource provider and a stub for a virtual entity javascript resource (this just contains some code to set all records to read only for example). If you don't want to use this solution or want to use it as boiler plate for your own version you can build the project then use the plugin registration tool to register the plugin and create a datasource - MS provides documenation on this process here: 

Whichever way you install it, the next step is to go to settings->administration->virtual entity data sources and create a new reference to you SQL Server. To do this, click "New", select either "Dynamic Sql Virtual Entity Provider" or your own custom data provider if you chose to build and deploy it yourself. 

Give it a name, then: 

- for regular SQL connectionm populate the connection string - this is regular SQL connection format of something like "Server=tcp:your_server_name.database.windows.net,1433;Initial Catalog=your_database_name;Persist Security Info=False;User ID=your_user_id;Password=your_user_password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
- for azure AD connection (use azuread branch), enter the tenant ID, client id and secret. server name will be your_server_name.database.windows.net, database name obviously the name of the db.

You can disable the create, update and delete operations altogether here if you wish (i.e. just to be sure Sys admins don't do anything they shouldn't, though you probably want to lock down permissions for your login anyway) 

Once you have done the above, you can simply create a new entity as normal. So, create a new entity, check the "Virtual Entity" checkboc and choose the virtual entity datasource you set up in the previous step.

You'll have to provide the external name to your SQL server table (this can have a schemaname if you need), external collection name doesn't do anything but is mandatory so just set it to the same as your external name. Make sure to set the external name on your primary field, and once you have saved the entity go to fields and update the primary key field with the external name of your SQL ID column.

From here, just create attributes in the same sort of way and remember to set the external name.

In terms of what the connector is expecting for the different atribute types:
- Single Line of Text = VARCHAR or NVARCHAR column
- Optionset = an integer column. If your optionsets are managed external, d365 does allow you to enter an "External Type Name" in the attribute definition, so this is a way in to build something around that. In other words.. if you need it, feel free to add the functionality and create a PR :)
- Two Options = a bit column
- Whole Number = integer column
- Floating Point Number = double column
- Decimal = decimal column
- Currency = decimal column
- Multiple Lines of Text = VARCHAR or NVARCHAR column
- Date and Time = date time column
- Lookup = a uniqueidentifier column

These types are currently unsupported:
- Image 
- Multiselect 
- Customer (although - possibly D365 creates multiple columns for this in the entity metadata? I've not tested this)
- File

## Security and Auditing

The azuread branch of the codebase allows you to set an 'owner' attribute field on the remote data. This should be a GUID, and the code will apply some basic additional filtering to the data source by only retrieving data where the owner field Id on the virtual data is the current user, is owned by one of the users teams or the record belongs to a user in the same team as the current user.

Currently, this project doesn't do anything for auditing.



﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml;
using System.Linq;
using System.Windows.Forms;
using EAAddinFramework.EASpecific;
using System.Diagnostics;

using UML=TSF.UmlToolingFramework.UML;

namespace TSF.UmlToolingFramework.Wrappers.EA {
  public class Model : UML.UMLModel 
  {
    private global::EA.Repository wrappedModel;
    private IWin32Window _mainEAWindow;
    private bool? msAccessSyntax;
    /// <summary>
    /// the main EA window to use when opening properties dialogs
    /// </summary>
    public IWin32Window mainEAWindow
    {
    	get
    	{
    		if //(true)
    			(this._mainEAWindow == null)
    		{
	    		List<Process> allProcesses = new List<Process>( Process.GetProcesses());
		   		Process proc = allProcesses.Find(pr => pr.ProcessName == "EA");
		     	//if we don't find the process then we set the mainwindow to null
		   		if (proc == null
		   		    || proc.MainWindowHandle == null)
		     	{
		     		this._mainEAWindow = null;
		     	}
		     	else
		     	{
		     		//found it. Create new WindowWrapper
		     		this._mainEAWindow  = new WindowWrapper(proc.MainWindowHandle);
		     	}
    		}
	     	return this._mainEAWindow;
    	}
    }

    /// Creates a model connecting to the first running instance of EA
    public Model(){
      object obj = Marshal.GetActiveObject("EA.App");
      global::EA.App eaApp = obj as global::EA.App;
      wrappedModel = eaApp.Repository;
    }

    /// constructor creates EAModel based on the given repository
    public Model(global::EA.Repository eaRepository){
      wrappedModel = eaRepository;
    }
    public UserControl addWindow(string title, string fullControlName)
    {
    	return this.wrappedModel.AddWindow(title,fullControlName) as UserControl;
    }
    /// the Element currently selected in EA
    public UML.Classes.Kernel.Element selectedElement {
      get {
        Object selectedItem;
        try
        {
            this.wrappedModel.GetContextItem(out selectedItem);
            return this.factory.createElement(selectedItem);
        }
        catch (COMException)
        {
            //something went wrong
            return null;
        }

        
      }
    	set
    	{
    		if (value is Package)
    		{
    			this.wrappedModel.ShowInProjectView(((Package)value).wrappedPackage);
    		}
    		else if (value is ElementWrapper)
    		{
    			this.wrappedModel.ShowInProjectView(((ElementWrapper)value).wrappedElement);
    		}
	        else if (value is Operation)
	        {
	            this.wrappedModel.ShowInProjectView(((Operation)value).wrappedOperation);
	        }
	        else if (value is Attribute)
	        {
	            this.wrappedModel.ShowInProjectView(((Attribute)value).wrappedAttribute);
	        }
	        else if (value is Parameter)
	        {
	        	Operation operation = (Operation)((Parameter)value).operation;
	        	this.wrappedModel.ShowInProjectView(operation.wrappedOperation);
	        }
    	}
    }
    
    /// returns the correct type of factory for this model
    public UML.UMLFactory factory {
      get { return Factory.getInstance(this); }
    }

    /// Finds the EA.Element with the given id and returns an EAElementwrapper 
    /// wrapping this element.
    public ElementWrapper getElementWrapperByID(int id){
      try{
        return this.factory.createElement
          (this.wrappedModel.GetElementByID(id)) as ElementWrapper;
      } catch( Exception )  {
        // element not found, return null
        return null;
      }
    }
    
    public void refreshDiagram(Diagram diagram)
    {
    	this.wrappedModel.ReloadDiagram(diagram.DiagramID);
    }

    public UML.Classes.Kernel.Element getElementByGUID(string GUIDstring)
    {
    	UML.Classes.Kernel.Element foundElement = null;
    	//first try elementwrapper
    	foundElement = this.getElementWrapperByGUID(GUIDstring);
    	//then try Attribute
    	if (foundElement == null)
    	{
    		foundElement = this.getAttributeByGUID(GUIDstring);
    	}
    	//then try Operation
    	if (foundElement == null)
    	{
    		foundElement = this.getOperationByGUID(GUIDstring);
    	}
    	//then try ConnectorWrapper
    	if (foundElement == null)
    	{
    		foundElement = this.getRelationByGUID(GUIDstring);
    	}
    	//then try Parameter
    	if (foundElement == null)
    	{
    		foundElement = this.getParameterByGUID(GUIDstring);
    	}
    	return foundElement;
    }
    /// <summary>
    /// Finds the EA.Element with the given GUID and returns an EAElementwrapper 
    /// wrapping this element.
    /// </summary>
    /// <param name="GUID">the GUID of the element</param>
    /// <returns>the element with the given GUID</returns>
    public ElementWrapper getElementWrapperByGUID(string GUID){
      try{
        return this.factory.createElement
          (this.wrappedModel.GetElementByGuid(GUID)) as ElementWrapper;
      } catch( Exception )  {
        // element not found, return null
        return null;
      }
    }
    /// <summary>
    /// returns the elementwrappers that are identified by the Object_ID's returned by the given query
    /// </summary>
    /// <param name="sqlQuery">query returning the Object_ID's</param>
    /// <returns>elementwrappers returned by the query</returns>
    public List<ElementWrapper> getElementWrappersByQuery(string sqlQuery)
    {
      // get the nodes with the name "ObjectID"
      XmlDocument xmlObjectIDs = this.SQLQuery(sqlQuery);
      XmlNodeList objectIDNodes = xmlObjectIDs.SelectNodes("//Object_ID");
      List<ElementWrapper> elements = new List<ElementWrapper>();
      
      foreach( XmlNode objectIDNode in objectIDNodes ) 
      {
      	ElementWrapper element = this.getElementWrapperByID(int.Parse(objectIDNode.InnerText));
        if (element != null)
        {
        	elements.Add(element);
        }
      }
      return elements;
    }
    /// <summary>
    /// returns the first [maxresults] items who's name starts with the given searchText
    /// </summary>
    /// <param name="searchText">the first part of the name to match</param>
    /// <param name="maxResults">the number of results required</param>
    /// <param name="elements">indicates whether elements should be selected</param>
    /// <param name="operations">indicates whether operations should be selected</param>
    /// <param name="attributes">indicates whether attributes should be selected</param>
    /// <param name="diagrams">indicates whether diagrams should be selected</param>
    /// <returns></returns>
    public List<UML.UMLItem> getQuickSearchResults(string searchText,int maxResults,bool elements, bool operations, bool attributes, bool diagrams)
    {
    	List<UML.UMLItem> results = new List<UML.UMLItem>();
    	if (elements)
	    {
	    	// get elements
	 		string SQLSelectElements = @"select  top "+maxResults + @" o.Object_ID from t_object o 
								where  o.name like '" +searchText +@"%'
								order by o.name, o.object_id";
	 		results.AddRange(this.getElementWrappersByQuery(SQLSelectElements).Cast<UML.UMLItem>().ToList());
    	}
    	if (operations)
    	{
	 		// get operations
	 		string SQLSelectOperations = @"select  top "+maxResults + @" o.OperationID from t_operation o 
								where  o.name like '" +searchText +@"%'
								order by o.name, o.OperationID";
	 		results.AddRange(this.getOperationsByQuery(SQLSelectOperations).Cast<UML.UMLItem>().ToList());
    	}
    	if (attributes)
    	{
	 		// get attributes
		 	string SQLSelectAttributes = @"select  top "+maxResults + @" a.ea_guid from t_Attribute a 
							where  a.name like '" +searchText +@"%'
							order by a.name, a.ea_guid";
	 		results.AddRange(this.getAttributesByQuery(SQLSelectAttributes).Cast<UML.UMLItem>().ToList());
    	}
    	if (diagrams)
    	{
	 		// get diagrams
	 		string SQLSelectDiagrams = @"select  top "+maxResults + @" d.Diagram_ID from t_Diagram d 
							where  d.name like '" +searchText +@"%'
							order by d.name, d.Diagram_ID";
	 		results.AddRange(this.getDiagramsByQuery(SQLSelectDiagrams).Cast<UML.UMLItem>().ToList());
    	}
 		
 		//sort alphabetically by name
 		results = results.OrderBy(x => x.name).ToList();
 		
 		//we need only the first maxresults
 		if (results.Count > maxResults)
 		{
 			results = results.GetRange(0,maxResults);
 		}
 		return results;
    }
    /// <summary>
    /// gets the Attribute with the given GUID
    /// </summary>
    /// <param name="GUID">the attribute's GUID</param>
    /// <returns>the Attribute with the given GUID</returns>
    public Attribute getAttributeByGUID (string GUID)
    {
    	try
    	{
    		return this.factory.createElement(this.wrappedModel.GetAttributeByGuid(GUID)) as Attribute;
    	}catch (Exception)
    	{
    		// attribute not found, return null
    		return null;
    	}
    	
    }
    /// <summary>
    /// gets the Attribute with the given ID
    /// </summary>
    /// <param name="attributID">the attribute's ID</param>
    /// <returns>the Attribute with the given ID</returns>
    public Attribute getAttributeByID (int attributID)
    {
    	try
    	{
    		return this.factory.createElement(this.wrappedModel.GetAttributeByID(attributID)) as Attribute;
    	}catch (Exception)
    	{
    		// attribute not found, return null
    		return null;
    	}
    	
    }
    /// <summary>
    /// gets the parameter by its GUID.
    /// This is a tricky one since EA doesn't provide a getParameterByGUID operation
    /// we have to first get the operation, then loop the pamarameters to find the one
    /// with the GUID
    /// </summary>
    /// <param name="GUID">the parameter's GUID</param>
    /// <returns>the Parameter with the given GUID</returns>
    public ParameterWrapper getParameterByGUID (string GUID)
    {
    	
    		//first need to get the operation for the parameter
    		string getOperationSQL = @"select p.OperationID from t_operationparams p
    									where p.ea_guid = '" + GUID +"'";
    		//first get the operation id
    		List<Operation> operations = this.getOperationsByQuery(getOperationSQL);
    		if (operations.Count > 0)
    		{
    			// the list of operations should only contain one operation
    			Operation operation = operations[0];
    			foreach ( ParameterWrapper parameter in operation.ownedParameters) {
    				if (parameter.ID == GUID) 
    				{
    					return parameter;
    				}
    			}
    		}
    	//parameter not found, return null
    	return null;
    }
    
    public UML.Diagrams.Diagram currentDiagram {
      get {
        return ((Factory)this.factory).createDiagram
          ( this.wrappedModel.GetCurrentDiagram() );
      }
      set { this.wrappedModel.OpenDiagram(((Diagram)value).DiagramID); }
    }
    
    internal Diagram getDiagramByID(int diagramID){
      return ((Factory)this.factory).createDiagram
        ( this.wrappedModel.GetDiagramByID(diagramID) ) as Diagram;
    }
    
    internal Diagram getDiagramByGUID(string diagramGUID){
      return ((Factory)this.factory).createDiagram
        ( this.wrappedModel.GetDiagramByGuid(diagramGUID) ) as Diagram;
    }

    internal ConnectorWrapper getRelationByID(int relationID) {
      return ((Factory)this.factory).createElement
        ( this.wrappedModel.GetConnectorByID(relationID)) as ConnectorWrapper;
    }
	
    internal ConnectorWrapper getRelationByGUID(string relationGUID) {
      return ((Factory)this.factory).createElement
        ( this.wrappedModel.GetConnectorByGuid(relationGUID)) as ConnectorWrapper;
    }
    
    internal List<ConnectorWrapper> getRelationsByQuery(string SQLQuery){
      // get the nodes with the name "Connector_ID"
      XmlDocument xmlrelationIDs = this.SQLQuery(SQLQuery);
      XmlNodeList relationIDNodes = 
        xmlrelationIDs.SelectNodes("//Connector_ID");
      List<ConnectorWrapper> relations = new List<ConnectorWrapper>();
      foreach( XmlNode relationIDNode in relationIDNodes ) {
        int relationID;
        if (int.TryParse( relationIDNode.InnerText, out relationID)) {
          ConnectorWrapper relation = this.getRelationByID(relationID);
          relations.Add(relation);
        }
      }
      return relations;
    }
    
      internal List<Attribute> getAttributesByQuery(string SQLQuery){
      // get the nodes with the name "ea_guid"
      XmlDocument xmlAttributeIDs = this.SQLQuery(SQLQuery);
      XmlNodeList attributeIDNodes = xmlAttributeIDs.SelectNodes("//ea_guid");
      List<Attribute> attributes = new List<Attribute>();
      
      foreach( XmlNode attributeIDNode in attributeIDNodes ) 
      {
        Attribute attribute = this.getAttributeByGUID(attributeIDNode.InnerText);
        if (attribute != null)
        {
        	attributes.Add(attribute);
        }
      }
      return attributes;
    }
    internal List<Parameter>getParametersByQuery(string SQLQuery)
    {
      // get the nodes with the name "ea_guid"
      XmlDocument xmlParameterIDs = this.SQLQuery(SQLQuery);
      XmlNodeList parameterIDNodes = xmlParameterIDs.SelectNodes("//ea_guid");
      List<Parameter> parameters = new List<Parameter>();
      
      foreach( XmlNode parameterIDNode in parameterIDNodes ) 
      {
        Parameter parameter = this.getParameterByGUID(parameterIDNode.InnerText);
        if (parameter != null)
        {
        	parameters.Add(parameter);
        }
      }
      return parameters;
    }
    internal List<Operation>getOperationsByQuery(string SQLQuery)
    {
      // get the nodes with the name "OperationID"
      XmlDocument xmlOperationIDs = this.SQLQuery(SQLQuery);
      XmlNodeList operationIDNodes = xmlOperationIDs.SelectNodes("//OperationID");
      List<Operation> operations = new List<Operation>();
      
      foreach( XmlNode operationIDNode in operationIDNodes ) 
      {
      	int operationID;
      	if (int.TryParse(operationIDNode.InnerText,out operationID))
      	{
        	Operation operation = this.getOperationByID(operationID) as Operation;
      	    if (operation != null)
		    {
		       	operations.Add(operation);
		    }    
      	}
 
      }
      return operations;
    }

    /// generic query operation on the model.
    /// Returns results in an xml format
    public XmlDocument SQLQuery(string sqlQuery)
    {
    	sqlQuery = this.formatSQL(sqlQuery);
    	XmlDocument results = new XmlDocument();
      	results.LoadXml(this.wrappedModel.SQLQuery(sqlQuery));
      	return results;
    }
    
    /// <summary>
    /// sets the correct wildcards depending on the database type.
    /// changes '%' into '*' if on ms access
    /// and _ into ? on msAccess
    /// </summary>
    /// <param name="sqlQuery">the original query</param>
    /// <returns>the fixed query</returns>
    private string formatSQL(string sqlQuery)
    {
    	if (!this.msAccessSyntax.HasValue)
    	{
    		this.msAccessSyntax = this.isMSAccessSyntax();
    	}
    	sqlQuery = replaceWildCards(sqlQuery,this.msAccessSyntax.Value);
    	return sqlQuery;
    }
    /// <summary>
    /// replace the wildcards in the given sql query string to match either MSAccess or ANSI syntax
    /// </summary>
    /// <param name="sqlQuery">the sql string to edit</param>
    /// <param name="msAccess">indicates if MSAccess syntax has to be used</param>
    /// <returns>the same sql query, but with its wildcards replaced according to the required syntax</returns>
    private string replaceWildCards(string sqlQuery, bool msAccess)
    {
    	int beginLike = sqlQuery.IndexOf("like",StringComparison.InvariantCultureIgnoreCase);
    	if (beginLike > 1 )
    	{
    		int beginString = sqlQuery.IndexOf("'",beginLike + "like".Length);
    		if (beginString > 0)
    		{
    			int endString = sqlQuery.IndexOf("'",beginString +1);
    			if (endString > beginString)
    			{
    				string originalLikeString = sqlQuery.Substring(beginString +1,endString - beginString );
    				string likeString = originalLikeString;
    				if (msAccess)
    				{
    					likeString = likeString.Replace('%','*');
    					likeString = likeString.Replace('_','?');
						likeString = likeString.Replace('^','!');
    				}
    				else
    				{
    					likeString = likeString.Replace('*','%');
    					likeString = likeString.Replace('?','_');
    					likeString = likeString.Replace('#','_');
						likeString = likeString.Replace('^','!');
    				}
    				string next = string.Empty;
    				if (endString < sqlQuery.Length)
    				{
    					next = replaceWildCards(sqlQuery.Substring(endString +1),msAccess);
    				}
    				sqlQuery = sqlQuery.Substring(0,beginString+1) + likeString + next;
    				    				
    			}
    		}
    	}
    	return sqlQuery;
    }
    /// <summary>
    /// Checks if the database needs MSAccess syntax for its SQL
    /// For a connection string, the DBMS repository type is identified by "DBType=n;" where n is a number corresponding to the DBMS type, as follows:
    /// 0 - MYSQL
    /// 1 - SQLSVR
    /// 2 - ADOJET -> ? and * 
    /// 3 - ORACLE
    /// 4 - POSTGRES
    /// 5 - ASA
    /// 7 - OPENEDGE
    /// 8 - ACCESS2007
    /// </summary>
    /// <returns></returns>
    public bool isMSAccessSyntax()
    {
    	string connectionString = this.wrappedModel.ConnectionString;
    	bool MSaccess = false;
    	//if it is a .eap file we check the size of it. if less then 1 MB then it is a shortcut file and we have to open it as a text file to find the actual connection string
    	if (connectionString.ToLower().EndsWith(".eap"))
    	{
    		System.IO.FileInfo fileInfo = new System.IO.FileInfo(connectionString);
    		if (fileInfo.Length > 1000)
    		{
    			//local .eap file, ms access syntax
    			MSaccess= true;
    		}
    		else
    		{
    			//open the file as a text file to find the connectionstring.
	            System.IO.FileStream fileStream = new System.IO.FileStream(connectionString, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite); 
	            System.IO.StreamReader reader = new System.IO.StreamReader(fileStream);
	            //replace connectionstring with the file contents
	            connectionString = reader.ReadToEnd();
	            reader.Close(); 
    		}
    	}
    	if (!connectionString.ToLower().EndsWith(".eap"))
    	{
    		if (connectionString.Contains("DBType=2"))
    		{
    			MSaccess = true;
    		}
    	}
    	return MSaccess;
    }
    /// <summary>
    /// saves unsaved changes to an opened diagram
    /// </summary>
    /// <param name="diagram">the diagram that is currently opened</param>
    public void saveOpenedDiagram(UML.Diagrams.Diagram diagram)
    {
    	this.wrappedModel.SaveDiagram(((Diagram)diagram).DiagramID);
    }
    public void adviseChange(UML.Classes.Kernel.Element element)
    {
    	if (element is ElementWrapper)
    	{
    		this.wrappedModel.AdviseElementChange(((ElementWrapper)element).id);
    	}
    	else if (element is ConnectorWrapper)
    	{
    		this.wrappedModel.AdviseConnectorChange(((ConnectorWrapper)element).id);
    	}
    }
    
    public void saveElement(UML.Classes.Kernel.Element element){
      ((Element)element).save();
    }

    public void saveDiagram(UML.Diagrams.Diagram diagram){
      throw new NotImplementedException();
    }

    internal ElementWrapper getElementWrapperByPackageID(int packageID)
    {
      return this.factory.createElement(this.wrappedModel.GetPackageByID(packageID)) as ElementWrapper;
    }

    //returns a list of diagrams according to the given query.
    //the given query should return a list of diagram id's
    internal List<Diagram> getDiagramsByQuery(string sqlGetDiagrams)
    {
        // get the nodes with the name "Diagram_ID"
        XmlDocument xmlDiagramIDs = this.SQLQuery(sqlGetDiagrams);
        XmlNodeList diagramIDNodes =
          xmlDiagramIDs.SelectNodes("//Diagram_ID");
        List<Diagram> diagrams = new List<Diagram>();
        foreach (XmlNode diagramIDNode in diagramIDNodes)
        {
            int diagramID;
            if (int.TryParse(diagramIDNode.InnerText, out diagramID))
            {
                Diagram diagram = this.getDiagramByID(diagramID);
                diagrams.Add(diagram);
            }
        }
        return diagrams;
    }

    internal UML.Classes.Kernel.Operation getOperationByGUID(string guid)
    {
    	Operation operation = this.factory.createElement(this.wrappedModel.GetMethodByGuid(guid)) as Operation;
    	if (operation == null)
    	{
    		List<OperationTag> tags = this.getOperationTagsWithValue(guid);
    		
    		foreach ( OperationTag tag in tags)
    		{
	    		if (tag != null && tag.name == "ea_guid")
	    		{
	    			operation = tag.owner as Operation;
	    		}
    		}
    	}
    	return operation;
        
    }
    internal UML.Classes.Kernel.Operation getOperationByID(int operationID)
    {
        return this.factory.createElement(this.wrappedModel.GetMethodByID(operationID)) as UML.Classes.Kernel.Operation;
    }

    
    internal void executeSQL(string SQLString)
    {
    	this.wrappedModel.Execute(SQLString);
    }
  	
	public void selectDiagram(Diagram diagram)
	{
		this.wrappedModel.ShowInProjectView(diagram.wrappedDiagram);
	}
	/// <summary>
	/// finds the item with the given guid
	/// </summary>
	/// <param name="guidString">the string with the guid</param>
	/// <returns>the item that is identified by the given GUID</returns>
  	public UML.UMLItem getItemFromGUID(string guidString)
  	{
  		try
  		{
  		UML.UMLItem foundItem = null;
  		foundItem = this.getElementByGUID(guidString);
  		if (foundItem == null) foundItem = this.getDiagramByGUID(guidString);
  		if (foundItem == null) foundItem = this.getAttributeByGUID(guidString);
  		if (foundItem == null) foundItem = this.getOperationByGUID(guidString);
  		if (foundItem == null) foundItem = this.getRelationByGUID(guidString);
  		return foundItem;
  		}
  		catch (System.Runtime.InteropServices.COMException e)
  		{
  			if (e.Message.Contains("Can't find matching ID"))
  			{
  				return null;
  			}
  			else
  			{
  				throw e;
  			}
  		}
  	}
  	
	public UML.UMLItem getItemFromFQN(string FQN)
	{
		//split the FQN in the different parts
		UML.UMLItem foundItem = null;
		foreach(UML.Classes.Kernel.Package package in  this.rootPackages)
		{
			
			foundItem = package.getItemFromRelativePath(FQN.Split('.').ToList<string>());
			if (foundItem != null)
			{
				break;
			}
		}
		return foundItem;
	}
  	
	public  HashSet<UML.Classes.Kernel.Package> rootPackages {
		get 
		{
			
			return new HashSet<UML.Classes.Kernel.Package>(this.factory.createElements(this.wrappedModel.Models).Cast<UML.Classes.Kernel.Package>());
		}
	}
  	
	public  UML.Diagrams.Diagram selectedDiagram
	{
		get
		{
			object item ;
			this.wrappedModel.GetTreeSelectedItem(out item);
			global::EA.Diagram diagram = item as global::EA.Diagram;
			if (diagram != null)
			{
				return this.factory.createDiagram(diagram);
			}
			else
			{
				return null;
			}
		}
		set
		{
			value.select();
			
		}
	}
	public  TSF.UmlToolingFramework.UML.UMLItem selectedItem {
		get {
		 	UML.UMLItem item = this.selectedElement;
		 	if (item == null)
		 	{
		 		item = this.selectedDiagram;
		 	}
		 	return item;
		}
		set 
		{
			if (value is UML.Diagrams.Diagram)
			{
				this.selectedDiagram = value as UML.Diagrams.Diagram;
			}
			else if (value is UML.Classes.Kernel.Element)
			{
				this.selectedElement = value as UML.Classes.Kernel.Element;
			}
		}
	}
  	
	public HashSet<UML.Profiles.TaggedValue> getTaggedValuesWithValue(string value)
	{
		HashSet<UML.Profiles.TaggedValue> taggedValues = new HashSet<TSF.UmlToolingFramework.UML.Profiles.TaggedValue>();
		
		//elements
		foreach (ElementTag elementTag in this.getElementTagsWithValue(value))
		{
			taggedValues.Add(elementTag);
		}
		//attribute
		foreach (AttributeTag attributeTag in this.getAttributeTagsWithValue(value))
		{
			taggedValues.Add(attributeTag);
		}
		//operations
		foreach (OperationTag operationTag in this.getOperationTagsWithValue(value))
		{
			taggedValues.Add(operationTag);
		}
		//parameters
		foreach (ParameterTag parameterTag in this.getParameterTagsWithValue(value))
		{
			taggedValues.Add(parameterTag);
		}
		//relations
		foreach (RelationTag relationTag in this.getRelationTagsWithValue(value))
		{
			taggedValues.Add(relationTag);
		}
		return taggedValues;
	}
	public HashSet<ElementTag> getElementTagsWithValue(string value)
	{
		HashSet<ElementTag> elementTags = new HashSet<ElementTag>();
		string sqlFindGUIDS = @"select ea_guid from t_objectproperties
								where value like '"+ value + "'";
		// get the nodes with the name "ea_guid"
	    XmlDocument xmlElementTagGUIDs = this.SQLQuery(sqlFindGUIDS);
	    XmlNodeList tagGUIDNodes = xmlElementTagGUIDs.SelectNodes("//ea_guid");
	    foreach (XmlNode guidNode in tagGUIDNodes) 
	    {
	    	ElementTag elementTag =  this.getElementTagByGUID(guidNode.InnerText);
	    	if (elementTag != null)
	    	{
	    		elementTags.Add(elementTag);
	    	}
	    }
	    return elementTags;
	}
	public ElementTag getElementTagByGUID(string GUID)
	{
		ElementTag elementTag = null;
		string getElementWrappers = @"select object_id from t_objectproperties
									where ea_guid like '"+ GUID +"'";
		XmlDocument xmlElementIDs = this.SQLQuery(getElementWrappers);
	    XmlNode elementNode = xmlElementIDs.SelectSingleNode("//object_id");
	    if (elementNode != null)
	    {
	    	int objectID ;
	    	if (int.TryParse(elementNode.InnerText,out objectID))
	    	{
	    		ElementWrapper owner = this.getElementWrapperByID(objectID);
	    		if(owner != null)
	    		{
		    		foreach (TaggedValue taggedValue in owner.taggedValues) 
		    		{
		    			if (taggedValue.ea_guid.Equals(GUID,StringComparison.InvariantCultureIgnoreCase))
		    			{
		    				elementTag = taggedValue as ElementTag;
		    			}
		    		}
	    		}
	    	}
	    	
	    }
	    return elementTag;
		
	}
	public List<AttributeTag> getAttributeTagsWithValue(string value)
	{
		List<AttributeTag> attributeTags = new List<AttributeTag>();
		string sqlFindGUIDS = @"select ea_guid from t_attributetag
								where value like '"+ value + "'";
		// get the nodes with the name "ea_guid"
	    XmlDocument xmlTagGUIDs = this.SQLQuery(sqlFindGUIDS);
	    XmlNodeList tagGUIDNodes = xmlTagGUIDs.SelectNodes("//ea_guid");
	    foreach (XmlNode guidNode in tagGUIDNodes) 
	    {
	    	AttributeTag attributeTag =  this.getAttributeTagByGUID(guidNode.InnerText);
	    	if (attributeTag != null)
	    	{
	    		attributeTags.Add(attributeTag);
	    	}
	    }
		return attributeTags;
	}
	public AttributeTag getAttributeTagByGUID(string GUID)
	{
		AttributeTag attributeTag = null;
		string getAttributes = @"select elementid from t_attributetag
									where ea_guid like '"+ GUID +"'";
		XmlDocument xmlElementIDs = this.SQLQuery(getAttributes);
	    XmlNode elementNode = xmlElementIDs.SelectSingleNode("//elementid");
	    if (elementNode != null)
	    {
	    	int objectID ;
	    	if (int.TryParse(elementNode.InnerText,out objectID))
	    	{
	    		Attribute owner = this.getAttributeByID(objectID);
	    		if(owner != null)
	    		{
		    		foreach (TaggedValue taggedValue in owner.taggedValues) 
		    		{
		    			if (taggedValue.ea_guid.Equals(GUID,StringComparison.InvariantCultureIgnoreCase))
		    			{
		    				attributeTag = taggedValue as AttributeTag;
		    			}
		    		}
	    		}
	    	}
	    }
	    return attributeTag;
	}
	
	public List<OperationTag> getOperationTagsWithValue(string value)
	{
		List<OperationTag> operationTags = new List<OperationTag>();
		string sqlFindGUIDS = @"select ea_guid from t_operationtag
								where value like '"+ value + "'";
		// get the nodes with the name "ea_guid"
	    XmlDocument xmlTagGUIDs = this.SQLQuery(sqlFindGUIDS);
	    XmlNodeList tagGUIDNodes = xmlTagGUIDs.SelectNodes("//ea_guid");
	    foreach (XmlNode guidNode in tagGUIDNodes) 
	    {
	    	OperationTag operationTag =  this.getOperationTagByGUID(guidNode.InnerText);
	    	if (operationTag != null)
	    	{
	    		operationTags.Add(operationTag);
	    	}
	    }
		return operationTags;
	}
	public OperationTag getOperationTagByGUID(string GUID)
	{
		OperationTag operationTag = null;
		string getOperations = @"select elementid from t_operationtag
									where ea_guid like '"+ GUID +"'";
		XmlDocument xmlElementIDs = this.SQLQuery(getOperations);
	    XmlNode elementNode = xmlElementIDs.SelectSingleNode("//elementid");
	    if (elementNode != null)
	    {
	    	int objectID ;
	    	if (int.TryParse(elementNode.InnerText,out objectID))
	    	{
	    		Operation owner = this.getOperationByID(objectID) as Operation;
	    		if(owner != null)
	    		{
		    		foreach (TaggedValue taggedValue in owner.taggedValues) 
		    		{
		    			if (taggedValue.ea_guid.Equals(GUID,StringComparison.InvariantCultureIgnoreCase))
		    			{
		    				operationTag = taggedValue as OperationTag;
		    			}
		    		}
	    		}
	    	}
	    }
	    return operationTag;
	}
	public List<ParameterTag> getParameterTagsWithValue(string value)
	{
		List<ParameterTag> parameterTags = new List<ParameterTag>();
		string sqlFindGUIDS = @"select propertyid from t_taggedvalue
								where notes like '"+ value + "'";
		// get the nodes with the name "ea_guid"
	    XmlDocument xmlTagGUIDs = this.SQLQuery(sqlFindGUIDS);
	    XmlNodeList tagGUIDNodes = xmlTagGUIDs.SelectNodes("//propertyid");
	    foreach (XmlNode guidNode in tagGUIDNodes) 
	    {
	    	ParameterTag parameterTag =  this.getParameterTagByGUID(guidNode.InnerText);
	    	if (parameterTag != null)
	    	{
	    		parameterTags.Add(parameterTag);
	    	}
	    }
		return parameterTags;
	}
	public ParameterTag getParameterTagByGUID(string GUID)
	{
		ParameterTag parameterTag = null;
		string getParameters = @"select elementid from t_taggedvalue
									where propertyid like '"+ GUID +"'";
		XmlDocument xmlElementIDs = this.SQLQuery(getParameters);
	    XmlNode elementNode = xmlElementIDs.SelectSingleNode("//elementid");
	    if (elementNode != null)
	    {
	    	
	    	if (elementNode.InnerText.Length > 0)
	    	{
	    		Parameter owner = this.getParameterByGUID(elementNode.InnerText);
	    		if(owner != null)
	    		{
		    		foreach (TaggedValue taggedValue in owner.taggedValues) 
		    		{
		    			if (taggedValue.ea_guid.Equals(GUID,StringComparison.InvariantCultureIgnoreCase))
		    			{
		    				parameterTag = taggedValue as ParameterTag;
		    			}
		    		}
	    		}
	    	}
	    }
	    return parameterTag;
	}
	public List<RelationTag> getRelationTagsWithValue(string value)
	{
		List<RelationTag> relationTags = new List<RelationTag>();
		string sqlFindGUIDS = @"select ea_guid from t_connectortag
								where value like '"+ value + "'";
		// get the nodes with the name "ea_guid"
	    XmlDocument xmlTagGUIDs = this.SQLQuery(sqlFindGUIDS);
	    XmlNodeList tagGUIDNodes = xmlTagGUIDs.SelectNodes("//ea_guid");
	    foreach (XmlNode guidNode in tagGUIDNodes) 
	    {
	    	RelationTag relationTag =  this.getRelationTagByGUID(guidNode.InnerText);
	    	if (relationTag != null)
	    	{
	    		relationTags.Add(relationTag);
	    	}
	    }
		return relationTags;
	}
	public RelationTag getRelationTagByGUID(string GUID)
	{
		RelationTag relationTag = null;
		string getRelations = @"select elementid from t_connectortag
									where ea_guid like '"+ GUID +"'";
		XmlDocument xmlElementIDs = this.SQLQuery(getRelations);
	    XmlNode elementNode = xmlElementIDs.SelectSingleNode("//elementid");
	    if (elementNode != null)
	    {
	    	int objectID ;
	    	if (int.TryParse(elementNode.InnerText,out objectID))
	    	{
	    		ConnectorWrapper owner = this.getRelationByID(objectID);
	    		if(owner != null)
	    		{
		    		foreach (TaggedValue taggedValue in owner.taggedValues) 
		    		{
		    			if (taggedValue.ea_guid.Equals(GUID,StringComparison.InvariantCultureIgnoreCase))
		    			{
		    				relationTag = taggedValue as RelationTag;
		    			}
		    		}
	    		}
	    	}
	    }
	    return relationTag;
	}
	/// <summary>
	/// all users defined in this model
	/// </summary>
	public List<User> users
	{
		get
		{
			
			List<User> userList = new List<User>();
			if (this.isSecurityEnabled)
			{
				string getUsers = "select u.UserLogin, u.FirstName, u.Surname from t_secuser u";
				XmlDocument users = this.SQLQuery(getUsers);
				foreach (XmlNode userNode in users.SelectNodes("//Row")) 
				{
					string login= string.Empty;
					string firstName = string.Empty;
					string lastName = string.Empty;
					foreach (XmlNode subNode in userNode.ChildNodes) 
					{
						switch (subNode.Name.ToLower()) 
						{
							case "userlogin":
								login = subNode.InnerText;
								break;
							case "firstname":
								firstName = subNode.InnerText;
								break;
							case "surname":
								lastName = subNode.InnerText;
								break;
						}
					}	
					userList.Add(((Factory)this.factory).createUser(login,firstName,lastName));				
				}
			}
			else
			{
				//security not enabled. List of all users is the list of all authors mentioned in the t_object table.
				string getUsers = "select distinct o.author from t_object o";
				XmlDocument users = this.SQLQuery(getUsers);
				foreach (XmlNode authorNode in users.SelectNodes("//author")) 
				{
					string login= authorNode.InnerText;
					string firstName = string.Empty;
					string lastName = string.Empty;
					//add user	
					userList.Add(((Factory)this.factory).createUser(login,firstName,lastName));	
				}
			}
			return userList;
		}
	}
	/// <summary>
	/// Contains the currently logged in user.
	/// Returns null is security not enabled.
	/// </summary>
	public User currentUser
	{
		get
		{
			string currentUserLogin = string.Empty;
			if (this.isSecurityEnabled)
			{
				currentUserLogin = this.wrappedModel.GetCurrentLoginUser(false);
			}
			else
			{
				currentUserLogin = Environment.UserName;
			}
			return this.users.Find(u => u.login.Equals(currentUserLogin,StringComparison.InvariantCultureIgnoreCase));
		}
	}
	/// <summary>
	/// The working sets defined in this model
	/// </summary>
	public List<WorkingSet> workingSets
	{
		get
		{
			List<WorkingSet> workingSetList = new List<WorkingSet>();
			string getWorkingSets = "select d.docid, d.DocName,d.Author from t_document d where d.DocType = 'WorkItem' order by d.Author, d.DocName";
			XmlDocument workingSets = this.SQLQuery(getWorkingSets);
			foreach (XmlNode workingSetNode in workingSets.SelectNodes("//Row")) 
			{
				string name= string.Empty;
				string id = string.Empty;
				string ownerFullName = string.Empty;
				foreach (XmlNode subNode in workingSetNode.ChildNodes) 
				{
					switch (subNode.Name.ToLower()) 
					{
						case "docid":
							id = subNode.InnerText;
							break;
						case "docname":
							name = subNode.InnerText;
							break;
						case "author":
							ownerFullName = subNode.InnerText;
							break;
					}
				}
				User owner = this.users.Find(u => u.fullName.Equals(ownerFullName,StringComparison.InvariantCultureIgnoreCase));
				workingSetList.Add(((Factory)this.factory).createWorkingSet(name,id,owner));				
			}
			return workingSetList;
		}
	}
	/// <summary>
	/// returns true if security is enabled in this model
	/// </summary>
	public bool isSecurityEnabled
	{
		get
		{
			try
			{
				this.wrappedModel.GetCurrentLoginUser();
				return true;
			}
			catch (System.Runtime.InteropServices.COMException e)
			{
				if (e.Message == "Security not enabled")
				{
					return false;
				}
				else 
				{
					throw e;
				}
			}
		}
	}
	/// <summary>
	/// opens the properties dialog for this item
	/// </summary>
	/// <param name="item">the item to open the properties dialog for</param>
	public void openProperties(UML.UMLItem item)
	{
		//get the type string
		string typeString = string.Empty;
		int itemID = 0;
		if (item is Package) 
		{
			typeString = "PKG";
			itemID = ((Package)item).packageID;
		}
		else if (item is ElementWrapper)
		{
			typeString = "ELM";
			itemID = ((ElementWrapper)item).id;
		}
		else if (item is Attribute)
		{
			typeString = "ATT";
			itemID = ((Attribute)item).id;
		}
		else if (item is Operation)
		{
			typeString = "OP";
			itemID = ((Operation)item).id;
		}
		else if (item is Diagram)
		{
			typeString = "DGM";
			itemID = ((Diagram)item).DiagramID;
		}
// TODO: figure out how to open the properties dialog for a connector.	
//		else if (item is ConnectorWrapper)
//		{
//			//typeString = "CON";
//			typeString = "MSG";
//			itemID = ((ConnectorWrapper)item).id;
//		}
		//open the actual dialog
		if (this.mainEAWindow != null
		    && typeString != string.Empty
		    && itemID != 0)
	    {
			string ret = this.wrappedModel.CustomCommand("CFormCommandHelper", "ProcessCommand", "Dlg=" + typeString + ";id=" + itemID.ToString() + ";hwnd=" + this.mainEAWindow.Handle);
	    }
	}
	
	
  }
}

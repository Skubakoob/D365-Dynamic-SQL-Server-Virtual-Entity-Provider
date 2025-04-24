// simple code snippet to disable all attributes on a form
var dsqlvep={};

dsqlvep.virtualEntityConfig_Onload=function(context){
	var formContext=context.getFormContext();
	
	formContext.getAttribute('dsqlvep_connectiontype').addOnChange(dsqlvep.virtualEntityConfig_OnConnectionTypeChange);
	formContext.getAttribute('dsqlvep_connectiontype').fireOnChange();	
}

dsqlvep.virtualEntityConfig_OnConnectionTypeChange=function(context){
    var formContext=context.getFormContext();
	//formContext.getControl(arg).getAttribute();
	let contype = formContext.getAttribute('dsqlvep_connectiontype').getValue();
	let controls={
		dsqlvep_connectionstring:{required: false, visible: false},
		dsqlvep_azureadclientid:{required: false, visible: false},
		dsqlvep_azureadclientsecret:{required: false, visible: false},
		dsqlvep_azureadtenantid:{required: false, visible: false},
		dsqlvep_servername:{required: false, visible: false},
		dsqlvep_databasename:{required: false, visible: false},
	};
	if(contype==588970000){ // SQL
		//formContext.getAttribute('dsqlvep_connectionstring').setRequiredLevel("required");
		controls.dsqlvep_connectionstring={required: true, visible: true}
		
		formContext.ui.tabs.get('tab_main').sections.get('section_sql').setVisible(true);
		formContext.ui.tabs.get('tab_main').sections.get('section_azuread').setVisible(false);
	}
	else if(contype==588970001){ // Azure AD
		controls.dsqlvep_azureadclientid={required: true, visible: true}
		controls.dsqlvep_azureadclientsecret={required: true, visible: true}
		controls.dsqlvep_azureadtenantid={required: true, visible: true}
		controls.dsqlvep_servername={required: true, visible: true}
		controls.dsqlvep_databasename={required: true, visible: true}
		
		formContext.ui.tabs.get('tab_main').sections.get('section_sql').setVisible(false);
		formContext.ui.tabs.get('tab_main').sections.get('section_azuread').setVisible(true);
	}
	
	Object.keys(controls).forEach(key=>{	        
		formContext.getAttribute(key).setRequiredLevel(controls[key].required?"required":"none");	
		formContext.getControl(key).setVisible(controls[key].visible);		
	});
		 
}

dsqlvep.makeAttributesReadOnly = function(context){
	var formContext=context.getFormContext();
	formContext.ui.controls.forEach(function(control,ix){
		control.setDisabled(true);
	});
}
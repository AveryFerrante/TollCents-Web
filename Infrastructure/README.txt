Pre-requisites:
	ssh-agent configured for accessing the server via the "tollcents" user
	appsettings.Development.json with necessary secrets
		Also - think about the "Authentication" folder and files

	Best ran as a powershell admin, otherwise stopping the ssh-agent server typically fails. This should be ran from
	within the Infrastructure folder, as it will create the necessary files in the correct location.

	Has parameter "-DeploySegmentData" to also copy the segment data file to the server.
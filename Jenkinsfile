pipeline {
    agent any
    environment {
        sdk = '.NET 6.0.100'
    }

    stages {
        stage('Prepare') {
            steps {
                echo 'Preparing.'
                dotnetRestore project: 'MagiCloud.sln', sdk: @{sdk}
            }
        }
        stage('Build') {
            steps {
                echo 'Building..'
                dotnetPublish configuration: 'Release', project: 'MagiCloud.csproj', runtime: 'linux-x64', sdk: @{sdk}, selfContained: false, workDirectory: 'MagiCloud' 
                dotnetPublish configuration: 'Release', project: 'MagiCloudWeb.csproj', runtime: 'linux-x64', sdk: @{sdk}, selfContained: false, workDirectory: 'MagiCloudWeb' 
                dotnetPublish configuration: 'Release', project: 'MagiConsole.csproj', sdk: @{sdk}, selfContained: false, workDirectory: 'MagiConsole' 
            }
        }
        stage('Test') {
            steps {
                echo 'Testing..'
            }
        }
        stage('Deploy') {
            steps {
                echo 'Deploying....'
            }
        }
    }
}
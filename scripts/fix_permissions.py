# find any files that have a different owner than the parent folder and set them to the same owner as the folder

from elasticsearch import Elasticsearch

# Set up Elasticsearch client
es = Elasticsearch('https://localhost:9200', basic_auth=('elastic', 'password'), verify_certs=False)  # replace with your Elasticsearch info

# Fetch files from Elasticsearch
file_results = es.search(index="magicloud_files",  query={"match_all": {}}, size=10000)

# Fetch folders from Elasticsearch
folder_results = es.search(index="magicloud_folders",  query={"match_all": {}}, size=10000)

# cache all folders and the userId of their owner
folders = {}
for hit in folder_results['hits']['hits']:
    folders[hit['_id']] = hit['_source']['userId']

# loop through the file results, find any files that have a different owner than the parent folder and set them to the same owner as the folder
for hit in file_results['hits']['hits']:
    file = hit['_source']
    if 'parentId' not in file or file['parentId'] is None:
        # this file is in the root folder, so skip it
        continue
    fileParentId = file['parentId']
    if file['userId'] != folders[fileParentId]:
        print('File ' + file['name'] + ' has a different owner than its parent folder')
        print('File owner: ' + file['userId'])
        print('Folder owner: ' + folders[fileParentId])
        print('Setting file owner to folder owner')
        file['userId'] = folders[fileParentId]
        es.index(index='magicloud_files', id=hit['_id'], document=file)
print('Done')
print('')



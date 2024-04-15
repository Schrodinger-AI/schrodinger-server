using System.Collections.Generic;
using AElf.Client.Dto;
using Hash = AElf.Types.Hash;
using MerklePath = AElf.Types.MerklePath;
using MerklePathNode = AElf.Types.MerklePathNode;

namespace SchrodingerServer.Helper;

public static class ContractConvertHelper
{
    public static MerklePath ConvertMerklePath(MerklePathDto merklePathDto)
    {
        var merklePath = new MerklePath();
        foreach (var node in merklePathDto.MerklePathNodes)
        {
            merklePath.MerklePathNodes.Add(new MerklePathNode
            {
                Hash = new Hash { Value = Hash.LoadFromHex(node.Hash).Value },
                IsLeftChildNode = node.IsLeftChildNode
            });
        }

        return merklePath;
    }

    public static MerklePathDto ConvertMerklePathDto(MerklePath merklePath)
    {
        var merklePathDto = new MerklePathDto()
        {
            MerklePathNodes = new List<MerklePathNodeDto>()
        };
        foreach (var node in merklePath.MerklePathNodes)
        {
            merklePathDto.MerklePathNodes.Add(new MerklePathNodeDto
            {
                Hash = node.Hash.ToHex(),
                IsLeftChildNode = node.IsLeftChildNode
            });
        }

        return merklePathDto;
    }
}
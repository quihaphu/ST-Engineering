import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import { ApiError, deleteProduct, getCategories, getProducts } from '../api/client'
import { useDebouncedValue } from '../hooks/useDebouncedValue'

export function ProductListPage() {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(0)
  const [pageSize, setPageSize] = useState(10)
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const debouncedSearch = useDebouncedValue(search)

  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: getCategories,
  })

  const productsQuery = useQuery({
    queryKey: ['products', page + 1, pageSize, debouncedSearch, categoryId],
    queryFn: () =>
      getProducts({
        page: page + 1,
        pageSize,
        search: debouncedSearch || undefined,
        categoryId: categoryId || undefined,
      }),
  })

  const deleteMutation = useMutation({
    mutationFn: deleteProduct,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['products'] })
    },
  })

  const items = productsQuery.data?.items ?? []
  const totalCount = productsQuery.data?.totalCount ?? 0

  return (
    <Stack spacing={2}>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={2}>
        <Typography variant="h4" component="h1">
          Products
        </Typography>
        <Button component={RouterLink} to="/products/new" variant="contained">
          Create product
        </Button>
      </Stack>

      <Paper sx={{ p: 2 }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
          <TextField
            label="Search"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value)
              setPage(0)
            }}
            fullWidth
            placeholder="Name or SKU"
          />
          <FormControl fullWidth>
            <InputLabel id="category-filter-label">Category</InputLabel>
            <Select
              labelId="category-filter-label"
              label="Category"
              value={categoryId}
              onChange={(e) => {
                setCategoryId(e.target.value)
                setPage(0)
              }}
            >
              <MenuItem value="">All</MenuItem>
              {(categoriesQuery.data ?? []).map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Stack>
      </Paper>

      {productsQuery.isLoading && (
        <Box display="flex" justifyContent="center" py={6}>
          <CircularProgress />
        </Box>
      )}

      {productsQuery.isError && (
        <Alert severity="error">
          {productsQuery.error instanceof ApiError
            ? productsQuery.error.title
            : 'Failed to load products.'}
        </Alert>
      )}

      {deleteMutation.isError && (
        <Alert severity="error">
          {deleteMutation.error instanceof ApiError
            ? deleteMutation.error.title
            : 'Failed to delete product.'}
        </Alert>
      )}

      {productsQuery.isSuccess && items.length === 0 && (
        <Alert severity="info">No products found. Create one to get started.</Alert>
      )}

      {productsQuery.isSuccess && items.length > 0 && (
        <Paper>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Category</TableCell>
                <TableCell align="right">Min price</TableCell>
                <TableCell align="right">Stock</TableCell>
                <TableCell align="right">Variants</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((product) => (
                <TableRow key={product.id} hover>
                  <TableCell>{product.name}</TableCell>
                  <TableCell>{product.categoryName}</TableCell>
                  <TableCell align="right">
                    {product.minPrice != null ? product.minPrice.toFixed(2) : '—'}
                  </TableCell>
                  <TableCell align="right">{product.totalStock}</TableCell>
                  <TableCell align="right">{product.variantCount}</TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} justifyContent="flex-end">
                      <Button
                        component={RouterLink}
                        to={`/products/${product.id}/edit`}
                        size="small"
                      >
                        Edit
                      </Button>
                      <Button
                        color="error"
                        size="small"
                        disabled={deleteMutation.isPending}
                        onClick={() => {
                          if (
                            window.confirm(
                              `Deactivate "${product.name}"? This soft-deletes the product.`,
                            )
                          ) {
                            deleteMutation.mutate(product.id)
                          }
                        }}
                      >
                        Delete
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={totalCount}
            page={page}
            onPageChange={(_, next) => setPage(next)}
            rowsPerPage={pageSize}
            onRowsPerPageChange={(e) => {
              setPageSize(parseInt(e.target.value, 10))
              setPage(0)
            }}
            rowsPerPageOptions={[5, 10, 25]}
          />
        </Paper>
      )}
    </Stack>
  )
}
